import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { assetsApi, authApi, alertsApi, branchesApi, pricingApi, whatsappApi } from '@/api/client';
import { formatCurrency } from '@/hooks/useSessions';
import { hasPermission, Permissions } from '@/lib/permissions';
import { useAuthStore } from '@/store';
import { SessionMode, TimeUnit, WatchingBilling, PaymentAccountType, NotificationChannel } from '@/types';
import type { BranchDetail, BranchPaymentAccount, Device, PricingPlan, Room, VenueAssetType } from '@/types';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { Icon, type IconName } from '@/components/ui/Icons';
import { Input } from '@/components/ui/Input';
import { Modal } from '@/components/ui/Modal';
import { PageHeader } from '@/components/ui/PageHelpers';
import { PageLoader } from '@/components/ui/PageLoader';

type Tab = 'branches' | 'rooms' | 'devices' | 'venueAssets' | 'pricing' | 'payments' | 'whatsapp' | 'alerts' | 'maintenance';

export function SettingsPage() {
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const activeBranchId = useAuthStore((s) => s.activeBranchId);
  const setAuth = useAuthStore((s) => s.setAuth);
  const queryClient = useQueryClient();
  const canManageAssets = hasPermission(user, Permissions.AssetsManage);
  const canManageSettings = hasPermission(user, Permissions.SettingsManage);
  const isMaster = !!user?.isMaster;
  const [tab, setTab] = useState<Tab>(isMaster ? 'branches' : 'rooms');

  const [branchOpen, setBranchOpen] = useState(false);
  const [editingBranch, setEditingBranch] = useState<BranchDetail | null>(null);
  const [branchName, setBranchName] = useState('');
  const [branchAddress, setBranchAddress] = useState('');
  const [branchPhone, setBranchPhone] = useState('');
  const [branchPrefix, setBranchPrefix] = useState('');
  const [branchIsActive, setBranchIsActive] = useState(true);

  type DraftAccount = { key: string; accountType: number; label: string; accountNumber: string };
  const [draftAccounts, setDraftAccounts] = useState<DraftAccount[]>([]);
  const [paymentsMsg, setPaymentsMsg] = useState('');
  const [whatsappMsg, setWhatsappMsg] = useState('');
  const [whatsappError, setWhatsappError] = useState('');
  const [waCountryCode, setWaCountryCode] = useState('20');
  const [waTestPhone, setWaTestPhone] = useState('');
  const [waTestMessage, setWaTestMessage] = useState('');
  const lastSavedWaSessionRef = useRef<string | null>(null);

  const [roomOpen, setRoomOpen] = useState(false);
  const [editingRoom, setEditingRoom] = useState<Room | null>(null);
  const [roomName, setRoomName] = useState('');
  const [roomNumber, setRoomNumber] = useState('');
  const [roomCapacity, setRoomCapacity] = useState('10');

  const [deviceOpen, setDeviceOpen] = useState(false);
  const [editingDevice, setEditingDevice] = useState<Device | null>(null);
  const [deviceName, setDeviceName] = useState('');
  const [deviceRoomId, setDeviceRoomId] = useState('');
  const [deviceIsActive, setDeviceIsActive] = useState(true);
  const [ctrlTypeId, setCtrlTypeId] = useState('');
  const [ctrlQty, setCtrlQty] = useState('2');

  const [planOpen, setPlanOpen] = useState(false);
  const [editingPlan, setEditingPlan] = useState<PricingPlan | null>(null);
  const [planIsActive, setPlanIsActive] = useState(true);
  const [planName, setPlanName] = useState('');
  const [planMode, setPlanMode] = useState<number>(SessionMode.Gaming);
  const [planUnit, setPlanUnit] = useState<number>(TimeUnit.PerHour);
  const [planWatchingBilling, setPlanWatchingBilling] = useState<number>(WatchingBilling.PerPerson);
  const [planRate, setPlanRate] = useState('50');
  const [planCoupleRate, setPlanCoupleRate] = useState('75');
  const [planIsPackage, setPlanIsPackage] = useState(false);
  const [planPackageHours, setPlanPackageHours] = useState('5');
  const [planPackagePrice, setPlanPackagePrice] = useState('');
  const [planPackageCouplePrice, setPlanPackageCouplePrice] = useState('');
  const [error, setError] = useState('');

  const [assetTypeOpen, setAssetTypeOpen] = useState(false);
  const [editingAssetType, setEditingAssetType] = useState<VenueAssetType | null>(null);
  const [assetTypeName, setAssetTypeName] = useState('');
  const [assetTotalQty, setAssetTotalQty] = useState('1');
  const [assetWorkingQty, setAssetWorkingQty] = useState('1');
  const [roomAssetTypeId, setRoomAssetTypeId] = useState('');
  const [roomAssetQty, setRoomAssetQty] = useState('2');
  const [draftRoomAssets, setDraftRoomAssets] = useState<
    { venueAssetTypeId: string; quantity: number; workingCount: number }[]
  >([]);

  const [smtpHost, setSmtpHost] = useState('smtp.gmail.com');
  const [smtpPort, setSmtpPort] = useState('587');
  const [smtpUsername, setSmtpUsername] = useState('');
  const [smtpPassword, setSmtpPassword] = useState('');
  const [senderDisplayName, setSenderDisplayName] = useState('');
  const [alertRecipientEmail, setAlertRecipientEmail] = useState('');
  const [ownerWhatsAppPhone, setOwnerWhatsAppPhone] = useState('');
  const [notifyLowStock, setNotifyLowStock] = useState(true);
  const [notifySubscription, setNotifySubscription] = useState(true);
  const [notifyDeviceMaintenance, setNotifyDeviceMaintenance] = useState(true);
  const [alertsMsg, setAlertsMsg] = useState('');
  const [maintDeviceId, setMaintDeviceId] = useState('');
  const [maintReason, setMaintReason] = useState('');
  const [maintOpen, setMaintOpen] = useState(false);

  const { data: rooms = [] } = useQuery({
    queryKey: ['rooms', user?.id, activeBranchId],
    queryFn: assetsApi.getRooms,
  });
  const { data: devices = [] } = useQuery({
    queryKey: ['devices', user?.id, activeBranchId],
    queryFn: () => assetsApi.getDevices(),
  });
  const { data: plans = [] } = useQuery({
    queryKey: ['all-plans', user?.id, activeBranchId],
    queryFn: () => pricingApi.getPlans(),
  });
  const { data: branches = [] } = useQuery({
    queryKey: ['branches', user?.id],
    queryFn: branchesApi.getAll,
    enabled: (isMaster && tab === 'branches') || tab === 'payments',
  });

  const { data: paymentBranch } = useQuery({
    queryKey: ['branch', activeBranchId],
    queryFn: () => branchesApi.getById(activeBranchId!),
    enabled: !!activeBranchId && tab === 'payments',
  });

  const { data: ctrlTypes = [] } = useQuery({
    queryKey: ['controller-types', user?.id],
    queryFn: assetsApi.getControllerTypes,
    enabled: canManageAssets,
  });

  const { data: venueAssetTypes = [] } = useQuery({
    queryKey: ['venue-asset-types', user?.id],
    queryFn: assetsApi.getVenueAssetTypes,
    enabled: canManageAssets && (tab === 'venueAssets' || tab === 'rooms'),
  });

  const { data: alertSettings } = useQuery({
    queryKey: ['alert-settings', user?.id],
    queryFn: alertsApi.getSettings,
    enabled: isMaster,
  });

  const { data: openMaintenance = [] } = useQuery({
    queryKey: ['device-maintenance', user?.id, activeBranchId],
    queryFn: alertsApi.getMaintenance,
    enabled: canManageAssets && tab === 'maintenance',
  });

  useEffect(() => {
    if (!alertSettings) return;
    setSmtpHost(alertSettings.smtpHost || 'smtp.gmail.com');
    setSmtpPort(String(alertSettings.smtpPort || 587));
    setSmtpUsername(alertSettings.smtpUsername || '');
    setSenderDisplayName(alertSettings.senderDisplayName || '');
    setAlertRecipientEmail(alertSettings.alertRecipientEmail || '');
    setOwnerWhatsAppPhone(alertSettings.ownerWhatsAppPhone || '');
    setNotifyLowStock(alertSettings.notifyLowStock);
    setNotifySubscription(alertSettings.notifySubscription);
    setNotifyDeviceMaintenance(alertSettings.notifyDeviceMaintenance);
    setSmtpPassword('');
  }, [alertSettings]);

  const canWhatsApp = isMaster || canManageSettings;

  const { data: waStatus, isLoading: waStatusLoading } = useQuery({
    queryKey: ['whatsapp', 'status'],
    queryFn: whatsappApi.status,
    enabled: canWhatsApp && tab === 'whatsapp',
    refetchInterval: 2000,
    meta: { silent: true },
  });

  const waReady = !!waStatus?.ready;

  const { data: waQr } = useQuery({
    queryKey: ['whatsapp', 'qr'],
    queryFn: whatsappApi.qr,
    enabled: canWhatsApp && tab === 'whatsapp' && !waReady,
    refetchInterval: 2000,
    meta: { silent: true },
  });

  const saveWaSessionMutation = useMutation({
    mutationFn: () => {
      if (!waStatus?.sessionId) throw new Error(t('whatsapp.noSessionId'));
      return whatsappApi.saveSession({
        sessionId: waStatus.sessionId,
        phone: waStatus.phone ?? undefined,
      });
    },
    onSuccess: () => {
      setWhatsappMsg(t('whatsapp.sessionSaved'));
      setWhatsappError('');
      queryClient.invalidateQueries({ queryKey: ['whatsapp'] });
    },
    onError: (e: Error) => {
      setWhatsappError(e.message);
      setWhatsappMsg('');
    },
  });

  const disconnectWaMutation = useMutation({
    mutationFn: () => whatsappApi.disconnect(),
    onSuccess: () => {
      setWhatsappMsg(t('whatsapp.disconnected'));
      setWhatsappError('');
      queryClient.invalidateQueries({ queryKey: ['whatsapp'] });
    },
    onError: (e: Error) => {
      setWhatsappError(e.message);
      setWhatsappMsg('');
    },
  });

  const testWaSendMutation = useMutation({
    mutationFn: () => {
      const local = waTestPhone.replace(/\D/g, '').replace(/^0+/, '');
      if (!waCountryCode.trim() || !local) throw new Error(t('whatsapp.phoneRequired'));
      if (!waTestMessage.trim()) throw new Error(t('whatsapp.messageRequired'));
      const phone = `${waCountryCode.replace(/\D/g, '')}${local}`;
      return whatsappApi.send({ phone, message: waTestMessage.trim() });
    },
    onSuccess: (res) => {
      if (res.success) {
        setWhatsappMsg(t('whatsapp.testSent'));
        setWhatsappError('');
      } else {
        setWhatsappError(res.error || t('common.error'));
        setWhatsappMsg('');
      }
    },
    onError: (e: Error) => {
      setWhatsappError(e.message);
      setWhatsappMsg('');
    },
  });

  // Auto-save when gateway reports ready + sessionId (once per session id)
  useEffect(() => {
    if (!waReady || !waStatus?.sessionId) return;
    if (lastSavedWaSessionRef.current === waStatus.sessionId) return;
    if (saveWaSessionMutation.isPending) return;
    lastSavedWaSessionRef.current = waStatus.sessionId;
    saveWaSessionMutation.mutate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [waReady, waStatus?.sessionId]);

  useEffect(() => {
    if (!waReady) lastSavedWaSessionRef.current = null;
  }, [waReady]);

  function qrImageSrc(): string | null {
    if (!waQr) return null;
    if (waQr.qrBase64) {
      return waQr.qrBase64.startsWith('data:')
        ? waQr.qrBase64
        : `data:image/png;base64,${waQr.qrBase64}`;
    }
    if (waQr.qr) {
      if (waQr.qr.startsWith('data:') || waQr.qr.startsWith('http')) return waQr.qr;
    }
    return null;
  }

  useEffect(() => {
    if (!paymentBranch) return;
    const rows = (paymentBranch.paymentAccounts ?? []).map((a: BranchPaymentAccount, i: number) => ({
      key: a.id || `legacy-${i}`,
      accountType: a.accountType,
      label: a.label ?? '',
      accountNumber: a.accountNumber,
    }));
    setDraftAccounts(rows);
    setPaymentsMsg('');
  }, [paymentBranch]);

  const paymentsMutation = useMutation({
    mutationFn: async () => {
      if (!paymentBranch || !activeBranchId) throw new Error(t('settings.selectBranchFirst'));
      return branchesApi.update(activeBranchId, {
        name: paymentBranch.name,
        address: paymentBranch.address ?? undefined,
        phone: paymentBranch.phone ?? undefined,
        invoicePrefix: paymentBranch.invoicePrefix,
        isActive: paymentBranch.isActive,
        paymentAccounts: draftAccounts
          .filter((a) => a.accountNumber.trim())
          .map((a, i) => ({
            accountType: a.accountType,
            label: a.label.trim() || null,
            accountNumber: a.accountNumber.trim(),
            sortOrder: i,
            isActive: true,
          })),
      });
    },
    onSuccess: () => {
      setPaymentsMsg(t('settings.paymentsSaved'));
      queryClient.invalidateQueries({ queryKey: ['branch', activeBranchId] });
      queryClient.invalidateQueries({ queryKey: ['branches'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  function addDraftAccount(accountType: number) {
    setDraftAccounts((prev) => [
      ...prev,
      { key: `${accountType}-${Date.now()}-${prev.length}`, accountType, label: '', accountNumber: '' },
    ]);
  }

  function updateDraftAccount(key: string, patch: Partial<DraftAccount>) {
    setDraftAccounts((prev) => prev.map((a) => (a.key === key ? { ...a, ...patch } : a)));
  }

  function removeDraftAccount(key: string) {
    setDraftAccounts((prev) => prev.filter((a) => a.key !== key));
  }

  const branchMutation = useMutation({
    mutationFn: async () => {
      if (editingBranch) {
        return branchesApi.update(editingBranch.id, {
          name: branchName.trim(),
          address: branchAddress.trim() || undefined,
          phone: branchPhone.trim() || undefined,
          invoicePrefix: branchPrefix.trim() || undefined,
          isActive: branchIsActive,
        });
      }
      const created = await branchesApi.create({
        name: branchName.trim(),
        address: branchAddress.trim() || undefined,
        phone: branchPhone.trim() || undefined,
        invoicePrefix: branchPrefix.trim() || undefined,
      });
      // Select the new branch (covers Master Admin creating their first venue).
      try {
        const res = await authApi.selectBranch(created.id);
        setAuth(res.accessToken, res.refreshToken, res.user, res.activeBranchId, res.accessTokenExpiresAt);
      } catch {
        // Branch created; user can refresh / select manually
      }
      return created;
    },
    onSuccess: () => {
      setBranchOpen(false);
      setEditingBranch(null);
      setBranchName('');
      setBranchAddress('');
      setBranchPhone('');
      setBranchPrefix('');
      setBranchIsActive(true);
      queryClient.invalidateQueries({ queryKey: ['branches'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteBranchMutation = useMutation({
    mutationFn: (id: string) => branchesApi.delete(id),
    onSuccess: (_data, id) => {
      setError('');
      queryClient.setQueriesData<import('@/types').Branch[]>(
        { queryKey: ['branches'] },
        (old) => old?.filter((b) => b.id !== id)
      );
      queryClient.invalidateQueries({ queryKey: ['branches'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const roomMutation = useMutation({
    mutationFn: () => {
      const data = {
        name: roomName,
        roomNumber: roomNumber || undefined,
        maxWatchingCapacity: Number(roomCapacity) || 10,
        assets: draftRoomAssets.length ? draftRoomAssets : undefined,
      };
      return editingRoom
        ? assetsApi.updateRoom(editingRoom.id, { ...data, isActive: editingRoom.isActive })
        : assetsApi.createRoom(data);
    },
    onSuccess: () => {
      setRoomOpen(false);
      setEditingRoom(null);
      setRoomName('');
      setRoomNumber('');
      setDraftRoomAssets([]);
      queryClient.invalidateQueries({ queryKey: ['rooms'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['venue-asset-types'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const assetTypeMutation = useMutation({
    mutationFn: () => {
      const data = {
        name: assetTypeName,
        totalQuantity: Number(assetTotalQty) || 0,
        workingCount: Number(assetWorkingQty) || 0,
      };
      return editingAssetType
        ? assetsApi.updateVenueAssetType(editingAssetType.id, {
            ...data,
            isActive: editingAssetType.isActive,
          })
        : assetsApi.createVenueAssetType(data);
    },
    onSuccess: () => {
      setAssetTypeOpen(false);
      setEditingAssetType(null);
      setAssetTypeName('');
      setAssetTotalQty('1');
      setAssetWorkingQty('1');
      queryClient.invalidateQueries({ queryKey: ['venue-asset-types'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteAssetTypeMutation = useMutation({
    mutationFn: (id: string) => assetsApi.deleteVenueAssetType(id),
    onSuccess: (_data, id) => {
      setError('');
      queryClient.setQueriesData<import('@/types').VenueAssetType[]>(
        { queryKey: ['venue-asset-types'] },
        (old) => old?.filter((a) => a.id !== id)
      );
      queryClient.invalidateQueries({ queryKey: ['venue-asset-types'] });
      queryClient.invalidateQueries({ queryKey: ['rooms'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteRoomMutation = useMutation({
    mutationFn: (id: string) => assetsApi.deleteRoom(id),
    onSuccess: (_data, id) => {
      setError('');
      queryClient.setQueriesData<import('@/types').Room[]>(
        { queryKey: ['rooms'] },
        (old) => old?.filter((r) => r.id !== id)
      );
      queryClient.invalidateQueries({ queryKey: ['rooms'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['venue-asset-types'] });
      queryClient.invalidateQueries({ queryKey: ['devices'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const alertsMutation = useMutation({
    mutationFn: () =>
      alertsApi.saveSettings({
        smtpHost,
        smtpPort: Number(smtpPort) || 587,
        smtpUsername,
        smtpPassword: smtpPassword || undefined,
        senderDisplayName,
        alertRecipientEmail,
        ownerWhatsAppPhone,
        notifyLowStock,
        notifySubscription,
        notifyDeviceMaintenance,
      }),
    onSuccess: () => {
      setAlertsMsg(t('settings.alertsSaved'));
      setSmtpPassword('');
      queryClient.invalidateQueries({ queryKey: ['alert-settings'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const testEmailMutation = useMutation({
    mutationFn: alertsApi.testEmail,
    onSuccess: () => setAlertsMsg(t('settings.testEmailSent')),
    onError: (e: Error) => setError(e.message),
  });

  const startMaintMutation = useMutation({
    mutationFn: () =>
      alertsApi.startMaintenance({
        deviceId: maintDeviceId,
        reason: maintReason,
      }),
    onSuccess: () => {
      setMaintOpen(false);
      setMaintReason('');
      queryClient.invalidateQueries({ queryKey: ['device-maintenance'] });
      queryClient.invalidateQueries({ queryKey: ['devices'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const completeMaintMutation = useMutation({
    mutationFn: (id: string) => alertsApi.completeMaintenance(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['device-maintenance'] });
      queryClient.invalidateQueries({ queryKey: ['devices'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const ensureCtrlType = async () => {
    if (ctrlTypeId) return ctrlTypeId;
    if (ctrlTypes[0]) return ctrlTypes[0].id;
    const created = await assetsApi.createControllerType({ name: 'Standard' });
    queryClient.invalidateQueries({ queryKey: ['controller-types'] });
    return created.id;
  };

  const deviceMutation = useMutation({
    mutationFn: async () => {
      const typeId = await ensureCtrlType();
      const qty = Number(ctrlQty) || 2;
      const controllers = [{ controllerTypeId: typeId, quantity: qty, workingCount: qty }];
      const name = deviceName.trim();
      if (editingDevice) {
        return assetsApi.updateDevice(editingDevice.id, {
          roomId: deviceRoomId || null,
          name,
          isActive: deviceIsActive,
          controllers,
        });
      }
      return assetsApi.createDevice({
        roomId: deviceRoomId || null,
        name,
        controllers,
      });
    },
    onSuccess: () => {
      setDeviceOpen(false);
      setEditingDevice(null);
      setDeviceName('');
      setDeviceIsActive(true);
      queryClient.invalidateQueries({ queryKey: ['devices'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deleteDeviceMutation = useMutation({
    mutationFn: (id: string) => assetsApi.deleteDevice(id),
    onSuccess: (_data, id) => {
      setError('');
      queryClient.setQueriesData<import('@/types').Device[]>(
        { queryKey: ['devices'] },
        (old) => old?.filter((d) => d.id !== id)
      );
      queryClient.invalidateQueries({ queryKey: ['devices'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const deletePlanMutation = useMutation({
    mutationFn: (id: string) => pricingApi.deletePlan(id),
    onSuccess: (_data, id) => {
      setError('');
      queryClient.setQueriesData<import('@/types').PricingPlan[]>(
        { queryKey: ['all-plans'] },
        (old) => old?.filter((p) => p.id !== id)
      );
      queryClient.invalidateQueries({ queryKey: ['all-plans'] });
      queryClient.invalidateQueries({ queryKey: ['plans'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  const planMutation = useMutation({
    mutationFn: () => {
      const isPkg = planMode === SessionMode.Gaming && planIsPackage;
      const unit = planUnit === TimeUnit.PerMinute ? TimeUnit.PerHour : planUnit;
      const data = {
        name: planName,
        sessionMode: planMode,
        timeUnit: isPkg ? TimeUnit.PerHour : unit,
        watchingBilling:
          planMode === SessionMode.Watching ? planWatchingBilling : WatchingBilling.PerPerson,
        vipSurchargePerHour: 0,
        gamingRates:
          planMode === SessionMode.Gaming
            ? isPkg
              ? [
                  { controllerCount: 1, rate: Number(planPackagePrice) },
                  {
                    controllerCount: 2,
                    rate: Number(planPackageCouplePrice) || Number(planPackagePrice),
                  },
                ]
              : [
                  { controllerCount: 1, rate: Number(planRate) },
                  { controllerCount: 2, rate: Number(planCoupleRate) || Number(planRate) * 1.5 },
                ]
            : undefined,
        watchingRates:
          planMode === SessionMode.Watching
            ? [{ ratePerPerson: Number(planRate) }]
            : undefined,
        packageDurationMinutes: isPkg ? Math.round(Number(planPackageHours) * 60) : null,
        packagePrice: isPkg ? Number(planPackagePrice) : null,
      };
      return editingPlan
        ? pricingApi.updatePlan(editingPlan.id, { ...data, isActive: planIsActive })
        : pricingApi.createPlan(data);
    },
    onSuccess: () => {
      setPlanOpen(false);
      setEditingPlan(null);
      setPlanName('');
      setPlanUnit(TimeUnit.PerHour);
      setPlanWatchingBilling(WatchingBilling.PerPerson);
      setPlanIsPackage(false);
      setPlanPackagePrice('');
      setPlanPackageCouplePrice('');
      setPlanIsActive(true);
      queryClient.invalidateQueries({ queryKey: ['all-plans'] });
      queryClient.invalidateQueries({ queryKey: ['plans'] });
    },
    onError: (e: Error) => setError(e.message),
  });

  function timeUnitLabel(unit: number) {
    if (unit === TimeUnit.PerGame) return t('settings.perGame');
    if (unit === TimeUnit.PerMinute) return t('settings.perMinute');
    return t('settings.perHour');
  }

  function planRateLabel() {
    if (planMode === SessionMode.Gaming) {
      return planUnit === TimeUnit.PerGame ? t('settings.rateLabelPerGame') : t('settings.rateLabelGaming');
    }
    if (planWatchingBilling === WatchingBilling.PerScreen) {
      return planUnit === TimeUnit.PerMinute
        ? t('settings.rateLabelPerScreenMinute')
        : t('settings.rateLabelPerScreen');
    }
    return t('settings.rateLabelPerPerson');
  }

  const tabs: { id: Tab; label: string; icon: IconName }[] = [
    ...(isMaster ? [{ id: 'branches' as const, label: t('settings.branches'), icon: 'branch' as const }] : []),
    { id: 'rooms', label: t('settings.rooms'), icon: 'room' },
    { id: 'devices', label: t('settings.devices'), icon: 'gaming' },
    { id: 'pricing', label: t('settings.pricing'), icon: 'pricing' },
    ...(canManageAssets
      ? [{ id: 'maintenance' as const, label: t('settings.maintenance'), icon: 'wrench' as const }]
      : []),
    ...((canManageSettings || isMaster)
      ? [{ id: 'payments' as const, label: t('settings.payments'), icon: 'accounting' as const }]
      : []),
    ...(canWhatsApp
      ? [{ id: 'whatsapp' as const, label: t('settings.whatsapp'), icon: 'user' as const }]
      : []),
  ];

  return (
    <div>
      <PageHeader title={t('settings.title')} />
      <p className="mb-6 max-w-2xl text-sm text-muted">{t('settings.hint')}</p>

      <div className="mb-6 flex flex-wrap gap-2">
        {tabs.map(({ id, label, icon }) => (
          <Button key={id} variant={tab === id ? 'primary' : 'secondary'} size="sm" onClick={() => setTab(id)}>
            <Icon name={icon} className="h-4 w-4" />
            {label}
          </Button>
        ))}
      </div>

      {error && (
        <div className="mb-4 rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger">
          {error}
        </div>
      )}

      {tab === 'branches' && isMaster && (
        <section className="space-y-4">
          <Button
            onClick={() => {
              setError('');
              setEditingBranch(null);
              setBranchName('');
              setBranchAddress('');
              setBranchPhone('');
              setBranchPrefix('');
              setBranchIsActive(true);
              setBranchOpen(true);
            }}
          >
            <Icon name="plus" className="h-4 w-4" />
            {t('settings.addBranch')}
          </Button>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {branches.map((b) => (
              <Card key={b.id}>
                <div className="flex items-start gap-3">
                  <span className="rounded-lg bg-primary/15 p-2 text-primary">
                    <Icon name="branch" className="h-5 w-5" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{b.name}</p>
                    <p className="text-xs text-muted">
                      {b.ownerName
                        ? `${t('settings.branchOwner')}: ${b.ownerName}`
                        : t('settings.branchOwnerUnknown')}
                    </p>
                    <p className="text-xs text-muted">
                      {b.invoicePrefix}
                      {b.address ? ` · ${b.address}` : ''}
                      {!b.isActive ? ` · ${t('users.inactive')}` : ''}
                      {b.id === activeBranchId ? ` · ${t('branch.active')}` : ''}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-col gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setError('');
                        setEditingBranch(b);
                        setBranchName(b.name);
                        setBranchAddress(b.address ?? '');
                        setBranchPhone(b.phone ?? '');
                        setBranchPrefix(b.invoicePrefix);
                        setBranchIsActive(b.isActive);
                        setBranchOpen(true);
                      }}
                    >
                      {t('users.edit')}
                    </Button>
                    <Button
                      variant="danger"
                      size="sm"
                      loading={deleteBranchMutation.isPending}
                      onClick={() => {
                        if (window.confirm(t('common.confirmDelete'))) {
                          deleteBranchMutation.mutate(b.id);
                        }
                      }}
                    >
                      {t('common.delete')}
                    </Button>
                  </div>
                </div>
              </Card>
            ))}
            {branches.length === 0 && <p className="text-muted">{t('settings.noBranches')}</p>}
          </div>
        </section>
      )}

      {tab === 'rooms' && (
        <section className="space-y-4">
          {canManageAssets && (
            <Button
              onClick={() => {
                setError('');
                setEditingRoom(null);
                setRoomName('');
                setRoomNumber('');
                setRoomCapacity('10');
                setDraftRoomAssets([]);
                setRoomOpen(true);
              }}
            >
              <Icon name="plus" className="h-4 w-4" />
              {t('settings.addRoom')}
            </Button>
          )}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {rooms.map((r) => (
              <Card key={r.id}>
                <div className="flex items-start gap-3">
                  <span className="rounded-lg bg-primary/15 p-2 text-primary">
                    <Icon name="room" className="h-5 w-5" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{r.name}</p>
                    <p className="text-xs text-muted">
                      {r.roomNumber ? `#${r.roomNumber} · ` : ''}
                      {r.deviceCount} {t('settings.devices').toLowerCase()}
                    </p>
                    {(r.assets?.length ?? 0) > 0 && (
                      <p className="mt-1 text-xs text-muted">
                        {r.assets.map((a) => `${a.assetTypeName} ${a.workingCount}/${a.quantity}`).join(' · ')}
                      </p>
                    )}
                  </div>
                  {canManageAssets && (
                    <div className="flex shrink-0 flex-col gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setError('');
                          setEditingRoom(r);
                          setRoomName(r.name);
                          setRoomNumber(r.roomNumber ?? '');
                          setRoomCapacity(String(r.maxWatchingCapacity));
                          setDraftRoomAssets(
                            (r.assets ?? []).map((a) => ({
                              venueAssetTypeId: a.venueAssetTypeId,
                              quantity: a.quantity,
                              workingCount: a.workingCount,
                            }))
                          );
                          setRoomOpen(true);
                        }}
                      >
                        {t('users.edit')}
                      </Button>
                      <Button
                        variant="danger"
                        size="sm"
                        loading={deleteRoomMutation.isPending}
                        onClick={() => {
                          if (window.confirm(t('common.confirmDelete'))) {
                            deleteRoomMutation.mutate(r.id);
                          }
                        }}
                      >
                        {t('common.delete')}
                      </Button>
                    </div>
                  )}
                </div>
              </Card>
            ))}
            {rooms.length === 0 && <p className="text-muted">{t('settings.noRooms')}</p>}
          </div>
        </section>
      )}

      {tab === 'venueAssets' && (
        <section className="space-y-4">
          {canManageAssets && (
            <Button
              onClick={() => {
                setError('');
                setEditingAssetType(null);
                setAssetTypeName('');
                setAssetTotalQty('1');
                setAssetWorkingQty('1');
                setAssetTypeOpen(true);
              }}
            >
              <Icon name="plus" className="h-4 w-4" />
              {t('settings.addAssetType')}
            </Button>
          )}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {venueAssetTypes.map((a) => (
              <Card key={a.id}>
                <div className="flex items-start gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{a.name}</p>
                    {a.description && <p className="text-xs text-muted">{a.description}</p>}
                    <p className="mt-1 text-xs text-muted">
                      {t('settings.assetTotalQty')}: {a.totalQuantity}
                      {' · '}
                      {t('settings.assetWorkingQty')}: {a.workingCount}
                    </p>
                    <p className="text-xs text-muted">
                      {t('settings.assetAssigned')}: {a.assignedQuantity}
                      {' · '}
                      {t('settings.assetAvailable')}: {Math.max(0, a.totalQuantity - a.assignedQuantity)}
                    </p>
                    {!a.isActive && <p className="mt-1 text-xs text-warning">{t('common.inactive')}</p>}
                  </div>
                  {canManageAssets && (
                    <div className="flex shrink-0 flex-col gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setError('');
                          setEditingAssetType(a);
                          setAssetTypeName(a.name);
                          setAssetTotalQty(String(a.totalQuantity));
                          setAssetWorkingQty(String(a.workingCount));
                          setAssetTypeOpen(true);
                        }}
                      >
                        {t('users.edit')}
                      </Button>
                      <Button
                        variant="danger"
                        size="sm"
                        loading={deleteAssetTypeMutation.isPending}
                        onClick={() => {
                          if (window.confirm(t('common.confirmDelete'))) {
                            deleteAssetTypeMutation.mutate(a.id);
                          }
                        }}
                      >
                        {t('common.delete')}
                      </Button>
                    </div>
                  )}
                </div>
              </Card>
            ))}
            {venueAssetTypes.length === 0 && <p className="text-muted">{t('settings.noAssetTypes')}</p>}
          </div>
        </section>
      )}

      {tab === 'devices' && (
        <section className="space-y-4">
          {canManageAssets && (
            <Button
              onClick={() => {
                setError('');
                setEditingDevice(null);
                setDeviceName('');
                setDeviceIsActive(true);
                setDeviceRoomId(rooms[0]?.id ?? '');
                setCtrlTypeId(ctrlTypes[0]?.id ?? '');
                setCtrlQty('2');
                setDeviceOpen(true);
              }}
            >
              <Icon name="plus" className="h-4 w-4" />
              {t('settings.addDevice')}
            </Button>
          )}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {devices.map((d) => (
              <Card
                key={d.id}
                className={!d.isActive ? 'border-danger/50 bg-danger/5' : undefined}
              >
                <div className="flex items-start gap-3">
                  <span className={`rounded-lg p-2 ${!d.isActive ? 'bg-danger/15 text-danger' : 'bg-accent/15 text-accent'}`}>
                    <Icon name="gaming" className="h-5 w-5" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className={`font-medium ${!d.isActive ? 'text-danger' : ''}`}>{d.name}</p>
                    <p className={`text-xs ${!d.isActive ? 'text-danger' : 'text-muted'}`}>
                      {d.roomName ? d.roomName : t('settings.noRoom')}
                      {!d.isActive ? ` · ${t('common.inactive')}` : ''}
                    </p>
                  </div>
                  {canManageAssets && (
                    <div className="flex shrink-0 flex-col gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setError('');
                          setEditingDevice(d);
                          setDeviceName(d.name);
                          setDeviceRoomId(d.roomId ?? '');
                          setDeviceIsActive(d.isActive);
                          setCtrlTypeId(ctrlTypes[0]?.id ?? '');
                          setCtrlQty(String(d.maxGamingPlayers || 2));
                          setDeviceOpen(true);
                        }}
                      >
                        {t('users.edit')}
                      </Button>
                      <Button
                        variant="danger"
                        size="sm"
                        loading={deleteDeviceMutation.isPending}
                        onClick={() => {
                          if (window.confirm(t('common.confirmDelete'))) {
                            deleteDeviceMutation.mutate(d.id);
                          }
                        }}
                      >
                        {t('common.delete')}
                      </Button>
                    </div>
                  )}
                </div>
              </Card>
            ))}
            {devices.length === 0 && <p className="text-muted">{t('settings.noDevices')}</p>}
          </div>
        </section>
      )}

      {tab === 'pricing' && (
        <section className="space-y-4">
          {canManageSettings && (
            <Button
              onClick={() => {
                setError('');
                setEditingPlan(null);
                setPlanName('');
                setPlanMode(SessionMode.Gaming);
                setPlanUnit(TimeUnit.PerHour);
                setPlanWatchingBilling(WatchingBilling.PerPerson);
                setPlanRate('50');
                setPlanCoupleRate('75');
                setPlanIsPackage(false);
                setPlanPackageHours('5');
                setPlanPackagePrice('');
                setPlanPackageCouplePrice('');
                setPlanIsActive(true);
                setPlanOpen(true);
              }}
            >
              <Icon name="plus" className="h-4 w-4" />
              {t('settings.addPlan')}
            </Button>
          )}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {plans.map((p) => (
              <Card key={p.id}>
                <div className="flex items-start gap-3">
                  <span className="rounded-lg bg-success/15 p-2 text-success">
                    <Icon name="pricing" className="h-5 w-5" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{p.name}</p>
                    <p className="text-xs text-muted">
                      {p.sessionMode === SessionMode.Gaming ? t('session.gaming') : t('session.watching')}
                      {' · '}
                      {p.sessionMode === SessionMode.Watching && p.watchingBilling === WatchingBilling.PerPerson
                        ? t('settings.flatSession')
                        : timeUnitLabel(p.timeUnit)}
                      {p.sessionMode === SessionMode.Watching
                        ? ` · ${p.watchingBilling === WatchingBilling.PerScreen ? t('settings.perScreen') : t('settings.perPerson')}`
                        : ''}
                    </p>
                    <p className="mt-1 text-sm text-accent">
                      {p.sessionMode === SessionMode.Gaming
                        ? p.packagePrice != null && p.packageDurationMinutes != null
                          ? `${t('settings.packageBadge')}: ${t('settings.individual')} ${formatCurrency(p.gamingRates.find((r) => r.controllerCount === 1)?.rate ?? p.packagePrice)} · ${t('settings.couple')} ${formatCurrency(p.gamingRates.find((r) => r.controllerCount === 2)?.rate ?? p.packagePrice)}`
                          : `${t('settings.individual')}: ${formatCurrency(p.gamingRates.find((r) => r.controllerCount === 1)?.rate ?? p.gamingRates[0]?.rate ?? 0)} · ${t('settings.couple')}: ${formatCurrency(p.gamingRates.find((r) => r.controllerCount === 2)?.rate ?? 0)}`
                        : formatCurrency(p.watchingRates[0]?.ratePerPerson ?? 0)}
                    </p>
                    {p.packagePrice != null && p.packageDurationMinutes != null && (
                      <p className="mt-1 text-xs font-medium text-primary">
                        {t('settings.packageBadge')}: {Math.round((p.packageDurationMinutes / 60) * 10) / 10}{t('session.hoursShort')}
                      </p>
                    )}
                    {!p.isActive && (
                      <p className="mt-1 text-xs text-warning">{t('common.inactive')}</p>
                    )}
                  </div>
                  {canManageSettings && (
                    <div className="flex shrink-0 flex-col gap-1">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => {
                          setError('');
                          setEditingPlan(p);
                          setPlanName(p.name);
                          setPlanMode(p.sessionMode);
                          setPlanUnit(p.timeUnit === TimeUnit.PerMinute ? TimeUnit.PerHour : p.timeUnit);
                          setPlanWatchingBilling(p.watchingBilling);
                          const isPkg = p.packagePrice != null && p.packageDurationMinutes != null;
                          setPlanRate(
                            String(
                              p.sessionMode === SessionMode.Gaming
                                ? p.gamingRates.find((r) => r.controllerCount === 1)?.rate ?? p.gamingRates[0]?.rate ?? 0
                                : p.watchingRates[0]?.ratePerPerson ?? 0
                            )
                          );
                          setPlanCoupleRate(String(p.gamingRates.find((r) => r.controllerCount === 2)?.rate ?? 0));
                          setPlanIsPackage(isPkg);
                          setPlanPackageHours(isPkg ? String(Math.round(((p.packageDurationMinutes ?? 0) / 60) * 10) / 10) : '5');
                          setPlanPackagePrice(
                            isPkg
                              ? String(p.gamingRates.find((r) => r.controllerCount === 1)?.rate ?? p.packagePrice ?? '')
                              : ''
                          );
                          setPlanPackageCouplePrice(
                            isPkg
                              ? String(p.gamingRates.find((r) => r.controllerCount === 2)?.rate ?? p.packagePrice ?? '')
                              : ''
                          );
                          setPlanIsActive(p.isActive);
                          setPlanOpen(true);
                        }}
                      >
                        {t('users.edit')}
                      </Button>
                      <Button
                        variant="danger"
                        size="sm"
                        loading={deletePlanMutation.isPending}
                        onClick={() => {
                          if (window.confirm(t('common.confirmDelete'))) {
                            deletePlanMutation.mutate(p.id);
                          }
                        }}
                      >
                        {t('common.delete')}
                      </Button>
                    </div>
                  )}
                </div>
              </Card>
            ))}
            {plans.length === 0 && <p className="text-muted">{t('settings.noPlans')}</p>}
          </div>
        </section>
      )}

      {tab === 'payments' && (
        <section className="space-y-4">
          {!activeBranchId ? (
            <p className="text-sm text-muted">{t('settings.selectBranchFirst')}</p>
          ) : (
            <Card className="max-w-2xl space-y-5">
              <div>
                <p className="font-medium">{paymentBranch?.name ?? '—'}</p>
                <p className="text-xs text-muted">{t('settings.paymentsHint')}</p>
              </div>

              <div className="space-y-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="text-sm font-semibold">{t('settings.bankAccounts')}</p>
                  <Button size="sm" variant="secondary" onClick={() => addDraftAccount(PaymentAccountType.BankTransfer)}>
                    <Icon name="plus" className="h-3.5 w-3.5" />
                    {t('settings.addBankAccount')}
                  </Button>
                </div>
                {draftAccounts
                  .filter((a) => a.accountType === PaymentAccountType.BankTransfer)
                  .map((a) => (
                    <div key={a.key} className="grid gap-2 rounded-lg border border-border p-3 sm:grid-cols-[1fr_1.4fr_auto]">
                      <Input
                        label={t('settings.accountLabel')}
                        value={a.label}
                        onChange={(e) => updateDraftAccount(a.key, { label: e.target.value })}
                        placeholder="InstaPay / CIB"
                      />
                      <Input
                        label={t('settings.accountNumber')}
                        value={a.accountNumber}
                        onChange={(e) => updateDraftAccount(a.key, { accountNumber: e.target.value })}
                        placeholder="01xxxxxxxxx"
                      />
                      <div className="flex items-end">
                        <Button size="sm" variant="ghost" onClick={() => removeDraftAccount(a.key)}>
                          {t('settings.removeAccount')}
                        </Button>
                      </div>
                    </div>
                  ))}
              </div>

              <div className="space-y-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="text-sm font-semibold">{t('settings.walletAccounts')}</p>
                  <Button size="sm" variant="secondary" onClick={() => addDraftAccount(PaymentAccountType.DigitalWallet)}>
                    <Icon name="plus" className="h-3.5 w-3.5" />
                    {t('settings.addWalletAccount')}
                  </Button>
                </div>
                {draftAccounts
                  .filter((a) => a.accountType === PaymentAccountType.DigitalWallet)
                  .map((a) => (
                    <div key={a.key} className="grid gap-2 rounded-lg border border-border p-3 sm:grid-cols-[1fr_1.4fr_auto]">
                      <Input
                        label={t('settings.accountLabel')}
                        value={a.label}
                        onChange={(e) => updateDraftAccount(a.key, { label: e.target.value })}
                        placeholder="Vodafone Cash"
                      />
                      <Input
                        label={t('settings.accountNumber')}
                        value={a.accountNumber}
                        onChange={(e) => updateDraftAccount(a.key, { accountNumber: e.target.value })}
                        placeholder="01xxxxxxxxx"
                      />
                      <div className="flex items-end">
                        <Button size="sm" variant="ghost" onClick={() => removeDraftAccount(a.key)}>
                          {t('settings.removeAccount')}
                        </Button>
                      </div>
                    </div>
                  ))}
              </div>

              {draftAccounts.length === 0 && (
                <p className="text-sm text-muted">{t('settings.noPaymentAccounts')}</p>
              )}
              {error && <p className="text-sm text-danger">{error}</p>}
              {paymentsMsg && <p className="text-sm text-success">{paymentsMsg}</p>}
              <Button
                loading={paymentsMutation.isPending}
                disabled={!canManageSettings && !isMaster}
                onClick={() => {
                  setError('');
                  setPaymentsMsg('');
                  paymentsMutation.mutate();
                }}
              >
                {t('settings.savePayments')}
              </Button>
            </Card>
          )}
        </section>
      )}

      {tab === 'alerts' && isMaster && alertSettings && alertSettings.allowedChannels !== NotificationChannel.None && (
        <section className="space-y-4">
          <p className="max-w-2xl text-sm text-muted">{t('settings.alertsHint')}</p>
          <Card className="max-w-2xl space-y-3">
            {(alertSettings.allowedChannels & NotificationChannel.Email) !== 0 && (
              <>
                <Input label={t('settings.smtpHost')} value={smtpHost} onChange={(e) => setSmtpHost(e.target.value)} />
                <Input label={t('settings.smtpPort')} type="number" value={smtpPort} onChange={(e) => setSmtpPort(e.target.value)} />
                <Input label={t('settings.smtpUsername')} value={smtpUsername} onChange={(e) => setSmtpUsername(e.target.value)} placeholder="venue@gmail.com" />
                <Input
                  label={t('settings.smtpPassword')}
                  type="password"
                  value={smtpPassword}
                  onChange={(e) => setSmtpPassword(e.target.value)}
                  placeholder={alertSettings?.hasSmtpPassword ? t('settings.smtpPasswordKeep') : ''}
                />
                <Input label={t('settings.senderDisplayName')} value={senderDisplayName} onChange={(e) => setSenderDisplayName(e.target.value)} />
                <Input label={t('settings.alertRecipientEmail')} value={alertRecipientEmail} onChange={(e) => setAlertRecipientEmail(e.target.value)} />
              </>
            )}
            {(alertSettings.allowedChannels & NotificationChannel.WhatsApp) !== 0 && (
              <Input label={t('settings.ownerWhatsAppPhone')} value={ownerWhatsAppPhone} onChange={(e) => setOwnerWhatsAppPhone(e.target.value)} placeholder="01xxxxxxxxx" />
            )}
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={notifyLowStock} onChange={(e) => setNotifyLowStock(e.target.checked)} />
              {t('settings.notifyLowStock')}
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={notifySubscription} onChange={(e) => setNotifySubscription(e.target.checked)} />
              {t('settings.notifySubscription')}
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={notifyDeviceMaintenance} onChange={(e) => setNotifyDeviceMaintenance(e.target.checked)} />
              {t('settings.notifyDeviceMaintenance')}
            </label>
            {error && <p className="text-sm text-danger">{error}</p>}
            {alertsMsg && <p className="text-sm text-success">{alertsMsg}</p>}
            <div className="flex flex-wrap gap-2">
              <Button
                loading={alertsMutation.isPending}
                onClick={() => {
                  setError('');
                  setAlertsMsg('');
                  alertsMutation.mutate();
                }}
              >
                {t('settings.saveAlerts')}
              </Button>
              {(alertSettings.allowedChannels & NotificationChannel.Email) !== 0 && (
                <Button
                  variant="secondary"
                  loading={testEmailMutation.isPending}
                  onClick={() => {
                    setError('');
                    setAlertsMsg('');
                    testEmailMutation.mutate();
                  }}
                >
                  {t('settings.testEmail')}
                </Button>
              )}
            </div>
          </Card>
        </section>
      )}

      {tab === 'maintenance' && (
        <section className="space-y-4">
          {canManageAssets && (
            <Button onClick={() => { setError(''); setMaintOpen(true); setMaintDeviceId(devices[0]?.id ?? ''); }}>
              <Icon name="plus" className="h-4 w-4" />
              {t('settings.startMaintenance')}
            </Button>
          )}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {openMaintenance.map((m) => (
              <Card key={m.id} className="space-y-2">
                <p className="font-medium">{m.deviceName}</p>
                <p className="text-xs text-muted">{m.roomName || t('settings.noRoom')}</p>
                <p className="text-sm">{m.reason}</p>
                <p className="text-xs text-warning">{t('settings.daysInMaintenance', { days: m.daysOpen })}</p>
                <Button size="sm" variant="secondary" loading={completeMaintMutation.isPending} onClick={() => completeMaintMutation.mutate(m.id)}>
                  {t('settings.completeMaintenance')}
                </Button>
              </Card>
            ))}
            {openMaintenance.length === 0 && <p className="text-muted">{t('settings.noMaintenance')}</p>}
          </div>
        </section>
      )}

      {tab === 'whatsapp' && canWhatsApp && (
        <section className="space-y-4">
          <p className="max-w-2xl text-sm text-muted">{t('whatsapp.hint')}</p>
          {waStatusLoading ? (
            <PageLoader />
          ) : (
            <Card>
              <div className="space-y-4">
                <div className="flex flex-wrap items-center gap-3">
                  <span
                    className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium ${
                      waReady
                        ? 'border-success/30 bg-success/20 text-success'
                        : 'border-warning/30 bg-warning/20 text-warning'
                    }`}
                  >
                    {waReady ? t('whatsapp.ready') : t('whatsapp.notReady')}
                  </span>
                  {waStatus?.phone && (
                    <span className="text-sm text-muted" dir="ltr">
                      {t('whatsapp.phone')}: {waStatus.phone}
                    </span>
                  )}
                </div>

                {!waReady && (
                  <div className="space-y-3">
                    <p className="text-sm text-muted">{t('whatsapp.scanQr')}</p>
                    <p className="text-xs text-warning">{t('whatsapp.qrFreshHint')}</p>
                    {qrImageSrc() ? (
                      <img
                        key={waQr?.qrBase64 || waQr?.qr || 'qr'}
                        src={qrImageSrc()!}
                        alt="WhatsApp QR"
                        className="mx-auto h-56 w-56 rounded-xl border border-border bg-white p-2"
                      />
                    ) : (
                      <p className="text-sm text-muted">{t('whatsapp.waitingQr')}</p>
                    )}
                  </div>
                )}

                <div className="flex flex-wrap gap-2">
                  {waReady && (
                    <Button
                      variant="danger"
                      loading={disconnectWaMutation.isPending}
                      onClick={() => {
                        setWhatsappMsg('');
                        setWhatsappError('');
                        disconnectWaMutation.mutate();
                      }}
                    >
                      {t('whatsapp.disconnect')}
                    </Button>
                  )}
                  {!waReady && waStatus?.sessionId && (
                    <Button
                      loading={saveWaSessionMutation.isPending}
                      onClick={() => {
                        setWhatsappMsg('');
                        setWhatsappError('');
                        saveWaSessionMutation.mutate();
                      }}
                    >
                      {t('whatsapp.saveSession')}
                    </Button>
                  )}
                </div>

                {waReady && (
                  <div className="space-y-3 border-t border-border pt-4">
                    <p className="text-sm font-medium">{t('whatsapp.testSendTitle')}</p>
                    <p className="text-xs text-muted">{t('whatsapp.testSendHint')}</p>
                    <div className="grid gap-3 sm:grid-cols-[120px_1fr]">
                      <div>
                        <label className="mb-1 block text-sm text-muted">{t('whatsapp.countryCode')}</label>
                        <select
                          className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
                          value={waCountryCode}
                          onChange={(e) => setWaCountryCode(e.target.value)}
                          dir="ltr"
                        >
                          <option value="20">+20 Egypt</option>
                          <option value="966">+966 KSA</option>
                          <option value="971">+971 UAE</option>
                          <option value="965">+965 Kuwait</option>
                          <option value="974">+974 Qatar</option>
                          <option value="973">+973 Bahrain</option>
                          <option value="968">+968 Oman</option>
                          <option value="962">+962 Jordan</option>
                          <option value="961">+961 Lebanon</option>
                          <option value="1">+1 USA/CA</option>
                        </select>
                      </div>
                      <Input
                        label={t('whatsapp.recipientPhone')}
                        value={waTestPhone}
                        onChange={(e) => setWaTestPhone(e.target.value)}
                        placeholder="1xxxxxxxxx"
                        dir="ltr"
                      />
                    </div>
                    <Input
                      label={t('whatsapp.testMessage')}
                      value={waTestMessage}
                      onChange={(e) => setWaTestMessage(e.target.value)}
                      placeholder={t('whatsapp.testMessagePlaceholder')}
                    />
                    <Button
                      loading={testWaSendMutation.isPending}
                      disabled={!waTestPhone.trim() || !waTestMessage.trim()}
                      onClick={() => {
                        setWhatsappMsg('');
                        setWhatsappError('');
                        testWaSendMutation.mutate();
                      }}
                    >
                      {t('whatsapp.sendTest')}
                    </Button>
                  </div>
                )}

                {whatsappMsg && <p className="text-sm text-success">{whatsappMsg}</p>}
                {whatsappError && <p className="text-sm text-danger">{whatsappError}</p>}
              </div>
            </Card>
          )}
        </section>
      )}

      <Modal
        open={branchOpen}
        onClose={() => {
          setBranchOpen(false);
          setEditingBranch(null);
        }}
        title={editingBranch ? t('users.edit') : t('settings.addBranch')}
      >
        <div className="space-y-3">
          <Input label={t('settings.branchName')} value={branchName} onChange={(e) => setBranchName(e.target.value)} />
          <Input label={t('settings.branchAddress')} value={branchAddress} onChange={(e) => setBranchAddress(e.target.value)} />
          <Input label={t('settings.branchPhone')} value={branchPhone} onChange={(e) => setBranchPhone(e.target.value)} />
          <Input label={t('settings.invoicePrefix')} value={branchPrefix} onChange={(e) => setBranchPrefix(e.target.value)} placeholder="INV" />
          {editingBranch && (
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={branchIsActive}
                onChange={(e) => setBranchIsActive(e.target.checked)}
              />
              {t('common.active')}
            </label>
          )}
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={branchMutation.isPending}
            disabled={!branchName.trim()}
            onClick={() => branchMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal
        open={roomOpen}
        onClose={() => {
          setRoomOpen(false);
          setEditingRoom(null);
        }}
        title={editingRoom ? t('settings.editRoom') : t('settings.addRoom')}
      >
        <div className="space-y-3">
          <Input label={t('settings.roomName')} value={roomName} onChange={(e) => setRoomName(e.target.value)} />
          <Input label={t('settings.roomNumber')} value={roomNumber} onChange={(e) => setRoomNumber(e.target.value)} />
          <Input label={t('settings.capacity')} type="number" value={roomCapacity} onChange={(e) => setRoomCapacity(e.target.value)} />
          <div className="space-y-2 rounded-lg border border-border p-3">
            <p className="text-sm font-medium">{t('settings.roomAssets')}</p>
            <p className="text-xs text-muted">{t('settings.assetsFirstHint')}</p>
            {venueAssetTypes.length > 0 ? (
              <>
                <div className="flex flex-wrap gap-2">
                  <select
                    className="rounded-lg border border-border bg-surface px-3 py-2 text-sm"
                    value={roomAssetTypeId}
                    onChange={(e) => setRoomAssetTypeId(e.target.value)}
                  >
                    <option value="">{t('settings.assetTypeName')}</option>
                    {venueAssetTypes.map((a) => {
                      const originalQty =
                        editingRoom?.assets?.find((x) => x.venueAssetTypeId === a.id)?.quantity ?? 0;
                      const available = Math.max(0, a.totalQuantity - a.assignedQuantity + originalQty);
                      return (
                        <option key={a.id} value={a.id}>
                          {a.name} ({t('settings.assetAvailable')}: {available})
                        </option>
                      );
                    })}
                  </select>
                  <Input
                    label={t('settings.assetQuantity')}
                    type="number"
                    value={roomAssetQty}
                    onChange={(e) => setRoomAssetQty(e.target.value)}
                  />
                  <Button
                    size="sm"
                    variant="secondary"
                    disabled={!roomAssetTypeId}
                    onClick={() => {
                      const type = venueAssetTypes.find((a) => a.id === roomAssetTypeId);
                      if (!type) return;
                      const originalQty =
                        editingRoom?.assets?.find((x) => x.venueAssetTypeId === roomAssetTypeId)?.quantity ?? 0;
                      const available = Math.max(0, type.totalQuantity - type.assignedQuantity + originalQty);
                      const qty = Math.min(Number(roomAssetQty) || 1, available);
                      if (qty <= 0) return;
                      setDraftRoomAssets((prev) => [
                        ...prev.filter((x) => x.venueAssetTypeId !== roomAssetTypeId),
                        { venueAssetTypeId: roomAssetTypeId, quantity: qty, workingCount: qty },
                      ]);
                      setRoomAssetTypeId('');
                    }}
                  >
                    {t('common.save')}
                  </Button>
                </div>
                {draftRoomAssets.map((a) => {
                  const typeName = venueAssetTypes.find((t) => t.id === a.venueAssetTypeId)?.name ?? a.venueAssetTypeId;
                  return (
                    <p key={a.venueAssetTypeId} className="text-xs text-muted">
                      {typeName}: {a.workingCount}/{a.quantity}
                    </p>
                  );
                })}
              </>
            ) : (
              <p className="text-xs text-muted">{t('settings.noAssetTypes')}</p>
            )}
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button className="w-full" loading={roomMutation.isPending} disabled={!roomName.trim()} onClick={() => roomMutation.mutate()}>
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal open={maintOpen} onClose={() => setMaintOpen(false)} title={t('settings.startMaintenance')}>
        <div className="space-y-3">
          <div>
            <label className="mb-1 block text-sm text-muted">{t('settings.devices')}</label>
            <select
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
              value={maintDeviceId}
              onChange={(e) => setMaintDeviceId(e.target.value)}
            >
              {devices.filter((d) => d.isActive).map((d) => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </div>
          <Input label={t('settings.maintenanceReason')} value={maintReason} onChange={(e) => setMaintReason(e.target.value)} />
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={startMaintMutation.isPending}
            disabled={!maintDeviceId || !maintReason.trim()}
            onClick={() => startMaintMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal
        open={assetTypeOpen}
        onClose={() => {
          setAssetTypeOpen(false);
          setEditingAssetType(null);
        }}
        title={editingAssetType ? t('users.edit') : t('settings.addAssetType')}
      >
        <div className="space-y-3">
          <Input label={t('settings.assetTypeName')} value={assetTypeName} onChange={(e) => setAssetTypeName(e.target.value)} />
          <div className="grid gap-3 sm:grid-cols-2">
            <Input
              label={t('settings.assetTotalQty')}
              type="number"
              value={assetTotalQty}
              onChange={(e) => setAssetTotalQty(e.target.value)}
            />
            <Input
              label={t('settings.assetWorkingQty')}
              type="number"
              value={assetWorkingQty}
              onChange={(e) => setAssetWorkingQty(e.target.value)}
            />
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={assetTypeMutation.isPending}
            disabled={!assetTypeName.trim()}
            onClick={() => assetTypeMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal
        open={deviceOpen}
        onClose={() => {
          setDeviceOpen(false);
          setEditingDevice(null);
        }}
        title={editingDevice ? t('users.edit') : t('settings.addDevice')}
      >
        <div className="space-y-3">
          <div>
            <label className="mb-1 block text-sm text-muted">{t('settings.room')}</label>
            <select
              className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
              value={deviceRoomId}
              onChange={(e) => setDeviceRoomId(e.target.value)}
            >
              <option value="">{t('settings.noRoom')}</option>
              {rooms.map((r) => (
                <option key={r.id} value={r.id}>{r.name}</option>
              ))}
            </select>
            <p className="mt-1 text-xs text-muted">{t('settings.deviceRoomOptional')}</p>
          </div>
          <Input label={t('settings.deviceName')} value={deviceName} onChange={(e) => setDeviceName(e.target.value)} />
          <Input label={t('dashboard.controllers')} type="number" value={ctrlQty} onChange={(e) => setCtrlQty(e.target.value)} />
          {editingDevice && (
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={deviceIsActive}
                onChange={(e) => setDeviceIsActive(e.target.checked)}
              />
              {t('common.active')}
            </label>
          )}
          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={deviceMutation.isPending}
            disabled={!deviceName.trim()}
            onClick={() => deviceMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <Modal
        open={planOpen}
        onClose={() => {
          setPlanOpen(false);
          setEditingPlan(null);
        }}
        title={editingPlan ? t('settings.editPlan') : t('settings.addPlan')}
      >
        <div className="space-y-3">
          <Input label={t('settings.planName')} value={planName} onChange={(e) => setPlanName(e.target.value)} />

          <div className="flex gap-2">
            <Button size="sm" variant={planMode === SessionMode.Gaming ? 'primary' : 'secondary'} onClick={() => {
              setPlanMode(SessionMode.Gaming);
              setPlanWatchingBilling(WatchingBilling.PerPerson);
            }}>
              {t('session.gaming')}
            </Button>
            <Button size="sm" variant={planMode === SessionMode.Watching ? 'primary' : 'secondary'} onClick={() => {
              setPlanMode(SessionMode.Watching);
              setPlanWatchingBilling(WatchingBilling.PerPerson);
              setPlanUnit(TimeUnit.PerHour);
              setPlanIsPackage(false);
            }}>
              {t('session.watching')}
            </Button>
          </div>
          {editingPlan && (
            <p className="text-xs text-muted">{t('settings.planModeEditHint')}</p>
          )}

          {planMode === SessionMode.Watching && (
            <div className="space-y-2">
              <p className="text-xs text-muted">{t('settings.pricingHintWatching')}</p>
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant={planWatchingBilling === WatchingBilling.PerPerson ? 'primary' : 'secondary'}
                  onClick={() => {
                    setPlanWatchingBilling(WatchingBilling.PerPerson);
                    setPlanUnit(TimeUnit.PerHour);
                  }}>
                  {t('settings.perPerson')}
                </Button>
                <Button size="sm" variant={planWatchingBilling === WatchingBilling.PerScreen ? 'primary' : 'secondary'}
                  onClick={() => {
                    setPlanWatchingBilling(WatchingBilling.PerScreen);
                    if (planUnit === TimeUnit.PerGame) setPlanUnit(TimeUnit.PerHour);
                  }}>
                  {t('settings.perScreen')}
                </Button>
              </div>
            </div>
          )}

          {planMode === SessionMode.Gaming && (
            <p className="text-xs text-muted">{t('settings.pricingHintGaming')}</p>
          )}

          {planMode === SessionMode.Gaming && !planIsPackage ? (
            <div className="flex flex-wrap gap-2">
              <Button size="sm" variant={planUnit === TimeUnit.PerHour ? 'primary' : 'secondary'} onClick={() => setPlanUnit(TimeUnit.PerHour)}>
                {t('settings.perHour')}
              </Button>
              <Button size="sm" variant={planUnit === TimeUnit.PerGame ? 'primary' : 'secondary'} onClick={() => {
                setPlanUnit(TimeUnit.PerGame);
                setPlanIsPackage(false);
              }}>
                {t('settings.perGame')}
              </Button>
            </div>
          ) : planMode === SessionMode.Watching && planWatchingBilling === WatchingBilling.PerScreen ? (
            <div className="flex flex-wrap gap-2">
              <Button size="sm" variant="primary">
                {t('settings.perHour')}
              </Button>
            </div>
          ) : planMode === SessionMode.Watching ? (
            <p className="text-xs text-muted">{t('settings.watchingFlatHint')}</p>
          ) : null}

          {planMode === SessionMode.Gaming && planUnit !== TimeUnit.PerGame && (
            <div className="space-y-2 rounded-lg border border-border p-3">
              <label className="flex items-center gap-2 text-sm font-medium">
                <input
                  type="checkbox"
                  checked={planIsPackage}
                  onChange={(e) => {
                    const on = e.target.checked;
                    setPlanIsPackage(on);
                    if (on) setPlanUnit(TimeUnit.PerHour);
                  }}
                />
                {t('settings.packageEnable')}
              </label>
              {planIsPackage && (
                <>
                  <Input
                    label={t('settings.packageDurationHours')}
                    type="number"
                    value={planPackageHours}
                    onChange={(e) => setPlanPackageHours(e.target.value)}
                  />
                  <div className="grid gap-3 sm:grid-cols-2">
                    <Input
                      label={t('settings.packageIndividualPrice')}
                      type="number"
                      value={planPackagePrice}
                      onChange={(e) => setPlanPackagePrice(e.target.value)}
                    />
                    <Input
                      label={t('settings.packageCouplePrice')}
                      type="number"
                      value={planPackageCouplePrice}
                      onChange={(e) => setPlanPackageCouplePrice(e.target.value)}
                    />
                  </div>
                  <p className="text-xs text-muted">{t('settings.packageHint')}</p>
                </>
              )}
            </div>
          )}

          {planMode === SessionMode.Gaming && !planIsPackage ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <Input label={t('settings.individualRate')} type="number" value={planRate} onChange={(e) => setPlanRate(e.target.value)} />
              <Input label={t('settings.coupleRate')} type="number" value={planCoupleRate} onChange={(e) => setPlanCoupleRate(e.target.value)} />
            </div>
          ) : planMode === SessionMode.Watching ? (
            <Input label={planRateLabel()} type="number" value={planRate} onChange={(e) => setPlanRate(e.target.value)} />
          ) : null}

          {editingPlan && (
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={planIsActive}
                onChange={(e) => setPlanIsActive(e.target.checked)}
              />
              {t('settings.planActive')}
            </label>
          )}

          {error && <p className="text-sm text-danger">{error}</p>}
          <Button
            className="w-full"
            loading={planMutation.isPending}
            disabled={
              !planName.trim() ||
              (planMode === SessionMode.Gaming && planIsPackage
                ? !(Number(planPackageHours) > 0) ||
                  !(Number(planPackagePrice) > 0) ||
                  !(Number(planPackageCouplePrice) > 0)
                : !planRate)
            }
            onClick={() => planMutation.mutate()}
          >
            {t('common.save')}
          </Button>
        </div>
      </Modal>

      <div className="mt-8">
        <Link to="/" className="inline-flex items-center gap-2 text-sm text-primary hover:underline">
          {t('settings.backToFloor')}
          <Icon name="arrow" className="h-4 w-4" />
        </Link>
      </div>
    </div>
  );
}
