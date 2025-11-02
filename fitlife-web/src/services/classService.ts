import api from './api'
import type { Class, ClassFilter } from '@/types/Class'

export const classService = {
  async getClasses(filters?: ClassFilter): Promise<{ data: Class[], total: number }> {
    const response = await api.get<{ success: boolean, data: Class[], total: number }>('/classes', {
      params: filters
    })
    return { data: response.data.data, total: response.data.total }
  },

  async getClassById(id: string): Promise<Class> {
    const response = await api.get<{ success: boolean, data: Class }>(`/classes/${id}`)
    return response.data.data
  },

  async bookClass(classId: string): Promise<void> {
    await api.post(`/classes/${classId}/book`)
  },
}
