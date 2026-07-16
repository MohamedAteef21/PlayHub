export const NotificationChannel = {
  None: 0,
  Email: 1,
  WhatsApp: 2,
  EmailAndWhatsApp: 3,
} as const;

export interface MasterAlertSettings {
  id: string;
  userId: string;
  smtpHost: string | null;
  smtpPort: number;
  smtpUsername: string | null;
  hasSmtpPassword: boolean;
  senderDisplayName: string | null;
  alertRecipientEmail: string | null;
  ownerWhatsAppPhone: string | null;
  notifyLowStock: boolean;
  notifySubscription: boolean;
  notifyDeviceMaintenance: boolean;
  allowedChannels: number;
}

export interface DeviceMaintenance {
  id: string;
  deviceId: string;
  deviceName: string;
  deviceIdentifier: string;
  roomName: string;
  reason: string;
  notes: string | null;
  startedAt: string;
  completedAt: string | null;
  reportedByName: string;
  daysOpen: number;
}
