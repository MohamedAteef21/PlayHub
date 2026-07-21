export const NotificationChannel = {
  None: 0,
  Email: 1,
  WhatsApp: 2,
  EmailAndWhatsApp: 3,
} as const;

export interface MasterAlertRecipient {
  id: string;
  email: string;
  displayName: string | null;
  notifyLowStock: boolean;
  notifySubscription: boolean;
  notifyDeviceMaintenance: boolean;
}

export interface MasterAlertSettings {
  id: string;
  userId: string;
  smtpUsername: string | null;
  hasSmtpPassword: boolean;
  senderDisplayName: string;
  ownerWhatsAppPhone: string | null;
  recipients: MasterAlertRecipient[];
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
