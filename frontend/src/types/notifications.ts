export type NotificationType =
  | "ScripReset"
  | "TimedNode"
  | "PriceAlert"
  | "PlanUpdate"
  | "General";

export type NotificationPriority = "Low" | "Medium" | "High";

export interface NotificationDto {
  id: string;
  type: NotificationType;
  priority: NotificationPriority;
  title: string;
  message: string;
  actionUrl: string | null;
  iconUrl: string | null;
  createdAt: string;
  expiresAt: string | null;
  metadata: Record<string, string>;
}
