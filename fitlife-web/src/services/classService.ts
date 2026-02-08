import api from './api'
import type { Class, ClassFilter } from '@/types/Class'

interface ApiResponse<T> {
  success: boolean
  data: T
  message?: string
}

export const classService = {
  async getClasses(filters?: ClassFilter): Promise<Class[]> {
    const params: Record<string, string | number | undefined> = {}
    if (filters?.type) params.type = filters.type
    if (filters?.level) params.level = filters.level
    if (filters?.startDate) params.startDate = filters.startDate
    if (filters?.pageSize) params.limit = filters.pageSize

    const response = await api.get<ApiResponse<Class[]>>('/classes', { params })
    return response.data.data
  },

  async getClassById(id: string): Promise<Class> {
    const response = await api.get<ApiResponse<Class>>(`/classes/${id}`)
    return response.data.data
  },

  async bookClass(classId: string): Promise<Class> {
    const response = await api.post<ApiResponse<Class>>(`/classes/${classId}/book`)
    return response.data.data
  },
}
