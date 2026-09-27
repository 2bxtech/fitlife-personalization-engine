import { describe, it, expect, beforeEach, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent, h } from 'vue'
import type { Recommendation } from '@/types/Recommendation'
import type { Class } from '@/types/Class'

vi.mock('@/services/demoService', () => ({
  demoService: { listPersonas: vi.fn(), startSession: vi.fn() },
}))
vi.mock('@/services/recommendationService', () => ({
  recommendationService: {
    getRecommendations: vi.fn(),
    refreshRecommendations: vi.fn(),
    trackEvent: vi.fn(),
    trackBatchEvents: vi.fn(),
  },
}))
vi.mock('@/services/classService', () => ({
  classService: { getClasses: vi.fn(), getClassById: vi.fn(), bookClass: vi.fn(), cancelBooking: vi.fn() },
}))

import { demoService } from '@/services/demoService'
import { recommendationService } from '@/services/recommendationService'
import { classService } from '@/services/classService'
import { useAuthStore } from '@/stores/auth'
import { useRecommendationStore } from '@/stores/recommendations'
import { useToast } from '@/composables/useToast'
import DashboardView from '@/views/DashboardView.vue'
import PersonaPicker from '@/components/demo/PersonaPicker.vue'

function jwt() {
  const body = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 }))
  return `${btoa('{}')}.${body}.sig`
}

const user = (id: string, firstName: string) => ({
  id,
  email: `${id}@example.com`,
  firstName,
  lastName: 'Demo',
  fitnessLevel: 'Intermediate',
  goals: [],
  preferredClassTypes: ['Yoga'],
  segment: 'YogaEnthusiast',
  createdAt: '2026-01-01T00:00:00Z',
})

const yoga: Class = {
  id: 'class_001',
  name: 'Morning Vinyasa Flow',
  type: 'Yoga',
  description: '',
  instructorId: 'inst_sarah',
  instructorName: 'Sarah Martinez',
  level: 'All Levels',
  startTime: '2030-01-01T06:00:00Z',
  durationMinutes: 60,
  capacity: 30,
  currentEnrollment: 18,
  availableSpots: 12,
  averageRating: 4.8,
  totalRatings: 10,
  weeklyBookings: 85,
  isActive: true,
  isBookedByCurrentUser: false,
}

const recommendation: Recommendation = {
  rank: 1,
  score: 85.6,
  reason: 'Yoga is one of your preferred class types.',
  factors: [
    { key: 'class_type', label: 'Preferred type', points: 15, detail: 'Yoga is one of your preferred class types' },
    { key: 'availability', label: 'Availability', points: -5, detail: '2 of 20 spots open' },
  ],
  class: yoga,
  generatedAt: '2026-01-01T00:00:00Z',
}

function router() {
  const blank = defineComponent({ render: () => h('div') })
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: blank },
      { path: '/dashboard', component: blank },
    ],
  })
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  sessionStorage.clear()
  vi.clearAllMocks()
  useToast().toasts.value.splice(0)
})

describe('demo sessions', () => {
  it('record the persona and clear the previous member’s recommendations', async () => {
    vi.mocked(recommendationService.getRecommendations).mockResolvedValueOnce([recommendation])
    const recommendations = useRecommendationStore()
    await recommendations.fetchRecommendations('previous')
    vi.mocked(demoService.startSession).mockResolvedValueOnce({ token: jwt(), user: user('user_002', 'Mike') })

    const auth = useAuthStore()
    await auth.startDemoSession('mike')

    expect(auth.personaId).toBe('mike')
    expect(auth.user?.firstName).toBe('Mike')
    expect(recommendations.recommendations).toEqual([])
  })

  it('explain an ended demo session instead of redirecting silently', async () => {
    vi.mocked(demoService.startSession).mockResolvedValueOnce({ token: jwt(), user: user('user_001', 'Sarah') })
    const auth = useAuthStore()
    await auth.startDemoSession('sarah')

    auth.expireSession()

    expect(auth.isAuthenticated).toBe(false)
    expect(auth.consumeSessionNotice()).toContain('demo session ended')
    expect(auth.consumeSessionNotice()).toBeNull()
  })
})

describe('persona picker', () => {
  it('signs in with one click and opens the dashboard', async () => {
    vi.mocked(demoService.listPersonas).mockResolvedValueOnce([
      { id: 'sarah', firstName: 'Sarah', fitnessLevel: 'Intermediate', preferredClassTypes: ['Yoga'], headline: 'h', summary: 's' },
    ])
    vi.mocked(demoService.startSession).mockResolvedValueOnce({ token: jwt(), user: user('user_001', 'Sarah') })
    const appRouter = router()
    await appRouter.push('/')
    const wrapper = mount(PersonaPicker, { global: { plugins: [appRouter] } })
    await flushPromises()

    await wrapper.get('[data-testid="persona-sarah"]').trigger('click')
    await flushPromises()

    expect(demoService.startSession).toHaveBeenCalledWith('sarah')
    expect(appRouter.currentRoute.value.path).toBe('/dashboard')
  })

  it('explains when demo mode is off', async () => {
    vi.mocked(demoService.listPersonas).mockResolvedValueOnce(null)
    const wrapper = mount(PersonaPicker, { global: { plugins: [router()] } })
    await flushPromises()

    expect(wrapper.text()).toContain('Demo mode is off')
  })
})

describe('dashboard booking', () => {
  async function mountDashboard() {
    vi.mocked(demoService.startSession).mockResolvedValueOnce({ token: jwt(), user: user('user_001', 'Sarah') })
    await useAuthStore().startDemoSession('sarah')
    vi.mocked(demoService.listPersonas).mockResolvedValue([])
    const wrapper = mount(DashboardView, { global: { plugins: [router()] } })
    await flushPromises()
    return wrapper
  }

  it('confirms a successful booking even when the follow-up re-rank fails', async () => {
    vi.mocked(recommendationService.getRecommendations)
      .mockResolvedValueOnce([recommendation])
      .mockRejectedValueOnce(new Error('Network Error'))
    vi.mocked(classService.bookClass).mockResolvedValueOnce({
      classData: { ...yoga, isBookedByCurrentUser: true, currentEnrollment: 19 },
      message: 'Class booked successfully',
    })
    const wrapper = await mountDashboard()

    await wrapper.get('article button').trigger('click')
    await flushPromises()

    const toasts = useToast().toasts.value
    expect(toasts.map((t) => t.type)).toEqual(['success'])
    expect(wrapper.text()).toContain('Booked')
    expect(wrapper.text()).toContain('Showing earlier results')
  })

  it('shows every factor with its points when the member asks why', async () => {
    vi.mocked(recommendationService.getRecommendations).mockResolvedValueOnce([recommendation])
    const wrapper = await mountDashboard()
    const toggle = wrapper.get('button[aria-controls]')

    expect(toggle.attributes('aria-expanded')).toBe('false')
    await toggle.trigger('click')

    expect(toggle.attributes('aria-expanded')).toBe('true')
    const table = wrapper.get('table')
    expect(table.text()).toContain('+15')
    expect(table.text()).toContain('-5')
    expect(table.text()).toContain('85.6')
  })

  it('offers a retry instead of an empty state when the first load fails', async () => {
    vi.mocked(recommendationService.getRecommendations).mockRejectedValueOnce(new Error('down'))
    const wrapper = await mountDashboard()

    expect(wrapper.text()).toContain('Recommendations could not be loaded')
    expect(wrapper.text()).not.toContain('No upcoming classes')
  })
})
