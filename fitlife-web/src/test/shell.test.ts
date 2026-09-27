import { describe, it, expect, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { defineComponent, h } from 'vue'
import App from '@/App.vue'
import Header from '@/components/layout/Header.vue'
import ToastNotification from '@/components/common/ToastNotification.vue'
import AppButton from '@/components/ui/AppButton.vue'
import { useToast } from '@/composables/useToast'

const Page = (title: string) => defineComponent({ render: () => h('h1', title) })

function testRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: Page('Home'), meta: { title: 'Home' } },
      { path: '/login', component: Page('Sign in'), meta: { title: 'Sign in' } },
    ],
  })
  router.afterEach((to) => {
    document.title = `${to.meta.title} · FitLife`
  })
  return router
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
})

describe('app shell', () => {
  it('offers a skip link to a focusable main landmark', async () => {
    const router = testRouter()
    await router.push('/')
    const wrapper = mount(App, { global: { plugins: [router] }, attachTo: document.body })

    expect(wrapper.get('a[href="#main"]').text()).toContain('Skip to main content')
    expect(wrapper.get('main#main').attributes('tabindex')).toBe('-1')
    wrapper.unmount()
  })

  it('moves focus to main and announces the page after client-side navigation', async () => {
    const router = testRouter()
    await router.push('/')
    const wrapper = mount(App, { global: { plugins: [router] }, attachTo: document.body })
    await flushPromises()

    await router.push('/login')
    await flushPromises()

    expect(document.activeElement).toBe(wrapper.get('main#main').element)
    expect(wrapper.get('[data-testid="route-announcer"]').text()).toBe('Sign in · FitLife')
    wrapper.unmount()
  })
})

describe('header menu', () => {
  it('exposes its state and closes on Escape, returning focus to the toggle', async () => {
    const router = testRouter()
    await router.push('/')
    const wrapper = mount(Header, { global: { plugins: [router] }, attachTo: document.body })
    const toggle = wrapper.get('button[aria-controls="mobile-menu"]')

    expect(toggle.attributes('aria-expanded')).toBe('false')
    await toggle.trigger('click')
    expect(toggle.attributes('aria-expanded')).toBe('true')

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await flushPromises()

    expect(toggle.attributes('aria-expanded')).toBe('false')
    expect(document.activeElement).toBe(toggle.element)
    wrapper.unmount()
  })
})

describe('toasts', () => {
  it('announce errors assertively and other notices politely', async () => {
    const toast = useToast()
    const wrapper = mount(ToastNotification)
    toast.error('Booking failed')
    toast.success('Booked')
    await flushPromises()

    const alerts = wrapper.get('[data-testid="toast-alerts"]')
    const notices = wrapper.get('[data-testid="toast-notices"]')
    expect(alerts.attributes('role')).toBe('alert')
    expect(alerts.text()).toContain('Booking failed')
    expect(notices.attributes('aria-live')).toBe('polite')
    expect(notices.text()).toContain('Booked')
    expect(notices.text()).not.toContain('Booking failed')
    toast.toasts.value.splice(0)
  })
})

describe('AppButton', () => {
  it('is disabled and marked busy while loading', () => {
    const wrapper = mount(AppButton, { props: { loading: true }, slots: { default: 'Book' } })
    const button = wrapper.get('button')
    expect(button.attributes('disabled')).toBeDefined()
    expect(button.attributes('aria-busy')).toBe('true')
  })
})
