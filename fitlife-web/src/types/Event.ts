export interface UserEvent {
  userId: string
  itemId: string
  itemType: string
  eventType: 'View' | 'Click' | 'Book' | 'Complete' | 'Cancel' | 'Rate'
  timestamp?: string
  metadata?: Record<string, any>
}

export interface BatchEventsRequest {
  events: UserEvent[]
}
