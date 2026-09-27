import { describe, it, expect, beforeEach, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent, h, ref } from 'vue'

vi.mock('@/services/userService', () => ({
  userService: { getUser: vi.fn(), updatePreferences: vi.fn() },
}))
vi.mock('@/services/recommendationService', () => ({
  recommendationService: {
    getRecommendations: vi.fn(),
    refreshRecommendations: vi.fn().mockResolvedValue([]),
    trackEvent: vi.fn(),
    trackBatchEvents: vi.fn(),
  },
}))

import { userService } from '@/services/userService'
import { useAuthStore } from '@/stores/auth'
import ProfileView from '@/views/ProfileView.vue'
import ChipCheckboxGroup from '@/components/ui/ChipCheckboxGroup.vue'

const member = {
  id: 'user_001',
  email: 'sarah@example.com',
  firstName: 'Sarah',
  lastName: 'Johnson',
  fitnessLevel: 'Intermediate',
  goals: ['Flexibility'],
  preferredClassTypes: ['Yoga'],
  segment: 'YogaEnthusiast',
  createdAt: '2026-01-01T00:00:00Z',
}

function mountProfile() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:p(.*)*', component: defineComponent({ render: () => h('div') }) }],
  })
  return mount(ProfileView, { global: { plugins: [router] } })
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  vi.clearAllMocks()
  useAuthStore().setUser({ ...member })
})

describe('profile', () => {
  it('shows the name read-only, because the API cannot change it', () => {
    const wrapper = mountProfile()
    expect(wrapper.text()).toContain('Sarah Johnson')
    expect(wrapper.find('input[type="text"]').exists()).toBe(false)
  })

  it('saves preference changes and keeps the signed-in member in sync', async () => {
    vi.mocked(userService.updatePreferences).mockResolvedValueOnce({
      ...member,
      preferredClassTypes: ['Yoga', 'Spin'],
    })
    const wrapper = mountProfile()
    const save = wrapper.get('button[type="submit"]')
    expect(save.attributes('disabled')).toBeDefined()

    await wrapper.get('input[type="checkbox"][value="Spin"]').setValue(true)
    expect(save.attributes('disabled')).toBeUndefined()
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(userService.updatePreferences).toHaveBeenCalledWith('user_001', {
      fitnessLevel: 'Intermediate',
      goals: ['Flexibility'],
      preferredClassTypes: ['Yoga', 'Spin'],
    })
    expect(useAuthStore().user?.preferredClassTypes).toEqual(['Yoga', 'Spin'])
    expect(wrapper.text()).toContain('See your updated recommendations')
  })

  it('reports a failed save without losing the edits', async () => {
    vi.mocked(userService.updatePreferences).mockRejectedValueOnce(new Error('Server unavailable'))
    const wrapper = mountProfile()
    await wrapper.get('input[type="checkbox"][value="Walking"]').setValue(true)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('Server unavailable')
    expect((wrapper.get('input[value="Walking"]').element as HTMLInputElement).checked).toBe(true)
  })
})

describe('profile session boundary', () => {
  it('ignores a save that completes after the member signed out', async () => {
    let finish!: () => void
    vi.mocked(userService.updatePreferences).mockImplementationOnce(
      () => new Promise((resolve) => (finish = () => resolve({ ...member, preferredClassTypes: ['Spin'] })))
    )
    const auth = useAuthStore()
    auth.token = 'token-a'
    const wrapper = mountProfile()
    await wrapper.get('input[type="checkbox"][value="Spin"]').setValue(true)
    await wrapper.get('form').trigger('submit')

    auth.logout()
    finish()
    await flushPromises()

    expect(auth.user).toBeNull()
    expect(localStorage.getItem('user')).toBeNull()
  })
})

describe('chip checkbox group', () => {
  it('is a named fieldset of real checkboxes', async () => {
    const Host = defineComponent({
      setup: () => ({ picked: ref<string[]>(['Yoga']) }),
      render() {
        return h(ChipCheckboxGroup, {
          legend: 'Preferred class types',
          options: ['Yoga', 'Spin'],
          modelValue: this.picked,
          'onUpdate:modelValue': (value: string[]) => (this.picked = value),
        })
      },
    })
    const wrapper = mount(Host)

    expect(wrapper.get('fieldset legend').text()).toBe('Preferred class types')
    const boxes = wrapper.findAll('input[type="checkbox"]')
    expect(boxes).toHaveLength(2)
    expect((boxes[0]!.element as HTMLInputElement).checked).toBe(true)
    await boxes[1]!.setValue(true)
    expect(wrapper.vm.picked).toEqual(['Yoga', 'Spin'])
  })
})
