import * as React from 'react';
import { Globe, ShieldCheck, Bell, KeyRound, Check } from 'lucide-react';
import { detectBrowserTimezone, updateTimezone, type TimezonePreference } from '../services/timezone';
import { authFetch } from '../auth';
import { useAsync } from '../hooks/useAsync';
import { useToast } from '../components/Toast';
import PageLoader from '../components/PageLoader';
import { fetchPreferences, savePreferences } from '../services/notificationService';
import { API } from '../config/constants';

interface NotificationPreference {
  eventType: string;
  emailEnabled: boolean;
  smsEnabled: boolean;
}

interface TimezoneOption {
  id: string;
  offsetMinutes: number;
  label: string;
}

type SettingsTab = 'profile' | 'security' | 'notifications' | 'linked-banks' | 'api-access';

const SETTINGS_TABS: { id: SettingsTab; label: string; icon: React.ReactNode }[] = [
  {
    id: 'profile',
    label: 'Profile',
    icon: (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
        <circle cx="12" cy="7" r="4"/>
      </svg>
    ),
  },
  {
    id: 'security',
    label: 'Security',
    icon: (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
      </svg>
    ),
  },
  {
    id: 'notifications',
    label: 'Notifications',
    icon: (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
        <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
      </svg>
    ),
  },
  {
    id: 'api-access',
    label: 'API Access',
    icon: (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="16 18 22 12 16 6"/>
        <polyline points="8 6 2 12 8 18"/>
      </svg>
    ),
  },
];

function buildTimezoneList(): TimezoneOption[] {
  let zones: string[];
  try {
    zones = (Intl as any).supportedValuesOf('timeZone');
  } catch {
    zones = [
      'UTC', 'America/New_York', 'America/Chicago', 'America/Denver',
      'America/Los_Angeles', 'America/Anchorage', 'Pacific/Honolulu',
      'Europe/London', 'Europe/Berlin', 'Europe/Paris', 'Europe/Moscow',
      'Asia/Tokyo', 'Asia/Shanghai', 'Asia/Kolkata', 'Asia/Dubai',
      'Australia/Sydney', 'Pacific/Auckland', 'America/Sao_Paulo',
      'Africa/Cairo', 'Africa/Johannesburg'
    ];
  }

  const now = new Date();
  return zones.map((tz) => {
    let offsetMinutes = 0;
    try {
      const parts = new Intl.DateTimeFormat('en-US', {
        timeZone: tz,
        timeZoneName: 'shortOffset'
      }).formatToParts(now);
      const tzPart = parts.find(p => p.type === 'timeZoneName')?.value ?? '';
      const match = tzPart.match(/GMT([+-]?)(\d{1,2})(?::(\d{2}))?/);
      if (match) {
        const sign = match[1] === '-' ? -1 : 1;
        const h = parseInt(match[2], 10);
        const m = parseInt(match[3] || '0', 10);
        offsetMinutes = sign * (h * 60 + m);
      }
    } catch { /* keep 0 */ }

    const sign = offsetMinutes >= 0 ? '+' : '-';
    const abs = Math.abs(offsetMinutes);
    const h = Math.floor(abs / 60);
    const min = abs % 60;
    const offsetStr = `UTC${sign}${String(h).padStart(2, '0')}:${String(min).padStart(2, '0')}`;
    return { id: tz, offsetMinutes, label: `(${offsetStr}) ${tz.replace(/_/g, ' ')}` };
  }).sort((a, b) => a.offsetMinutes - b.offsetMinutes || a.id.localeCompare(b.id));
}

const Settings: React.FC = () => {
  const [activeTab, setActiveTab] = React.useState<SettingsTab>('profile');
  const [timeZoneId, setTimeZoneId] = React.useState('');
  const [utcOffsetMinutes, setUtcOffsetMinutes] = React.useState<number | ''>('');
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [message, setMessage] = React.useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [browserTz, setBrowserTz] = React.useState<TimezonePreference | null>(null);

  const [search, setSearch] = React.useState('');
  const [dropdownOpen, setDropdownOpen] = React.useState(false);
  const [highlightIndex, setHighlightIndex] = React.useState(-1);
  const dropdownRef = React.useRef<HTMLDivElement>(null);
  const listRef = React.useRef<HTMLUListElement>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);

  const allTimezones = React.useMemo(() => buildTimezoneList(), []);

  const filteredTimezones = React.useMemo(() => {
    if (!search.trim()) return allTimezones;
    const q = search.toLowerCase().replace(/\s+/g, '');
    return allTimezones.filter(tz =>
      tz.id.toLowerCase().replace(/_/g, '').includes(q) ||
      tz.label.toLowerCase().replace(/\s+/g, '').includes(q)
    );
  }, [search, allTimezones]);

  const selectedLabel = React.useMemo(() => {
    if (!timeZoneId) return '';
    const match = allTimezones.find(tz => tz.id === timeZoneId);
    return match?.label ?? timeZoneId;
  }, [timeZoneId, allTimezones]);

  React.useEffect(() => {
    const detected = detectBrowserTimezone();
    setBrowserTz(detected);

    (async () => {
      try {
        const res = await authFetch(API.PROFILE);
        if (res.ok) {
          const profile = await res.json();
          setTimeZoneId(profile.timeZoneId ?? '');
          setUtcOffsetMinutes(profile.utcOffsetMinutes ?? '');
        }
      } catch {
        // ignore — fields will be blank
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  React.useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  React.useEffect(() => {
    if (dropdownOpen && highlightIndex >= 0 && listRef.current) {
      const item = listRef.current.children[highlightIndex] as HTMLElement;
      item?.scrollIntoView({ block: 'nearest' });
    }
  }, [highlightIndex, dropdownOpen]);

  const selectTimezone = (tz: TimezoneOption) => {
    setTimeZoneId(tz.id);
    setUtcOffsetMinutes(tz.offsetMinutes);
    setSearch('');
    setDropdownOpen(false);
    setMessage(null);
  };

  const handleDetect = () => {
    if (browserTz?.timeZoneId) {
      const match = allTimezones.find(tz => tz.id === browserTz.timeZoneId);
      if (match) {
        selectTimezone(match);
      } else {
        setTimeZoneId(browserTz.timeZoneId);
        setUtcOffsetMinutes(browserTz.utcOffsetMinutes ?? '');
      }
      setMessage(null);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!dropdownOpen) {
      if (e.key === 'ArrowDown' || e.key === 'Enter') {
        setDropdownOpen(true);
        setHighlightIndex(0);
        e.preventDefault();
      }
      return;
    }
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setHighlightIndex(i => Math.min(i + 1, filteredTimezones.length - 1));
        break;
      case 'ArrowUp':
        e.preventDefault();
        setHighlightIndex(i => Math.max(i - 1, 0));
        break;
      case 'Enter':
        e.preventDefault();
        if (highlightIndex >= 0 && highlightIndex < filteredTimezones.length) {
          selectTimezone(filteredTimezones[highlightIndex]);
        }
        break;
      case 'Escape':
        setDropdownOpen(false);
        break;
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setMessage(null);
    setSaving(true);
    try {
      const pref: TimezonePreference = {
        timeZoneId: timeZoneId.trim() || null,
        utcOffsetMinutes: utcOffsetMinutes === '' ? null : Number(utcOffsetMinutes)
      };
      await updateTimezone(pref);
      setMessage({ type: 'success', text: 'Timezone preference saved.' });
    } catch (err: any) {
      setMessage({ type: 'error', text: err.message || 'Failed to save timezone preference.' });
    } finally {
      setSaving(false);
    }
  };

  const formatOffset = (minutes: number): string => {
    const sign = minutes >= 0 ? '+' : '-';
    const abs = Math.abs(minutes);
    const h = Math.floor(abs / 60);
    const m = abs % 60;
    return `UTC${sign}${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
  };

  if (loading) {
    return (
      <div className="settings-page">
        <div className="settings-loading">
          <PageLoader />
        </div>
      </div>
    );
  }

  return (
    <div className="settings-page">
      <div className="settings-header">
        <h2>Settings</h2>
        <p>Manage your account preferences and configurations</p>
      </div>

      <div className="settings-layout">
        {/* Left Rail Navigation */}
        <nav className="settings-nav" role="tablist" aria-label="Settings sections">
          {SETTINGS_TABS.map(tab => (
            <button
              key={tab.id}
              role="tab"
              aria-selected={activeTab === tab.id}
              aria-controls={`panel-${tab.id}`}
              className={`settings-nav-item${activeTab === tab.id ? ' active' : ''}`}
              onClick={() => setActiveTab(tab.id)}
              type="button"
            >
              <span className="settings-nav-icon">{tab.icon}</span>
              <span className="settings-nav-label">{tab.label}</span>
            </button>
          ))}
        </nav>

        {/* Content Panel */}
        <div className="settings-content">
          {/* Profile Tab */}
          {activeTab === 'profile' && (
            <section
              id="panel-profile"
              role="tabpanel"
              aria-labelledby="tab-profile"
              className="settings-panel"
            >
              <div className="settings-section">
                <h3 className="settings-section-title">
                  <Globe size={18} color="currentColor" style={{ verticalAlign: 'middle', marginRight: 6 }} /> Timezone Preference
                </h3>
                <p className="settings-section-desc">
                  All transactions are recorded in Universal Coordinated Time (UTC). Your preference applies to display formatting only.
                </p>

                {browserTz?.timeZoneId && (
                  <div className="settings-detected-tz">
                    <strong>Detected browser timezone:</strong> {browserTz.timeZoneId}
                    {browserTz.utcOffsetMinutes !== null && (
                      <span> ({formatOffset(browserTz.utcOffsetMinutes)})</span>
                    )}
                    <button
                      type="button"
                      onClick={handleDetect}
                      className="btn btn-secondary"
                      style={{ marginLeft: '1rem', padding: '4px 12px', fontSize: '0.8rem' }}
                    >
                      Use this
                    </button>
                  </div>
                )}

                <form onSubmit={handleSave}>
                  <div className="form-group" ref={dropdownRef} style={{ position: 'relative' }}>
                    <label htmlFor="tzSearch">Timezone</label>
                    <div style={{ position: 'relative', width: '100%', maxWidth: 560 }}>
                      <input
                        ref={inputRef}
                        type="text"
                        id="tzSearch"
                        className="form-control"
                        autoComplete="off"
                        value={dropdownOpen ? search : selectedLabel}
                        onChange={(e) => {
                          setSearch(e.target.value);
                          setHighlightIndex(0);
                          if (!dropdownOpen) setDropdownOpen(true);
                        }}
                        onFocus={() => {
                          setSearch('');
                          setDropdownOpen(true);
                          setHighlightIndex(-1);
                        }}
                        onKeyDown={handleKeyDown}
                        placeholder="Search timezone…"
                        role="combobox"
                        aria-expanded={dropdownOpen}
                        aria-controls="tz-listbox"
                        aria-autocomplete="list"
                        aria-activedescendant={highlightIndex >= 0 ? `tz-option-${highlightIndex}` : undefined}
                        style={{
                          width: '100%',
                          padding: '.8rem 2.5rem .8rem 1rem',
                          border: '1px solid var(--border)',
                          borderRadius: 10,
                          fontSize: '1rem',
                          background: '#fff',
                          color: 'var(--text)',
                          transition: 'border-color .2s ease, box-shadow .2s ease',
                          cursor: 'text'
                        }}
                      />
                      <span
                        aria-hidden="true"
                        style={{
                          position: 'absolute',
                          right: 14,
                          top: '50%',
                          transform: `translateY(-50%) rotate(${dropdownOpen ? '180deg' : '0'})`,
                          transition: 'transform 0.2s',
                          pointerEvents: 'none',
                          fontSize: '0.7rem',
                          color: 'var(--muted, #6b7280)'
                        }}
                      >
                        ▼
                      </span>
                    </div>
                    {dropdownOpen && (
                      <ul
                        id="tz-listbox"
                        ref={listRef}
                        role="listbox"
                        style={{
                          position: 'absolute',
                          top: '100%',
                          left: 0,
                          right: 0,
                          maxWidth: 560,
                          maxHeight: 260,
                          overflowY: 'auto',
                          margin: '4px 0 0',
                          padding: 0,
                          listStyle: 'none',
                          background: '#fff',
                          border: '1px solid var(--border, #e5e7eb)',
                          borderRadius: 10,
                          boxShadow: '0 8px 24px rgba(17,24,39,0.12)',
                          zIndex: 50
                        }}
                      >
                        {filteredTimezones.length === 0 ? (
                          <li style={{ padding: '0.75rem 1rem', color: 'var(--muted, #6b7280)', fontSize: '0.9rem' }}>
                            No timezones found
                          </li>
                        ) : (
                          filteredTimezones.map((tz, i) => (
                            <li
                              key={tz.id}
                              id={`tz-option-${i}`}
                              role="option"
                              aria-selected={tz.id === timeZoneId}
                              onMouseDown={(e) => {
                                e.preventDefault();
                                selectTimezone(tz);
                              }}
                              onMouseEnter={() => setHighlightIndex(i)}
                              style={{
                                padding: '0.6rem 1rem',
                                fontSize: '0.9rem',
                                cursor: 'pointer',
                                background: i === highlightIndex
                                  ? 'var(--primary-bg, #eff6ff)'
                                  : tz.id === timeZoneId
                                    ? '#f0fdf4'
                                    : 'transparent',
                                color: 'var(--text, #111827)',
                                borderBottom: i < filteredTimezones.length - 1 ? '1px solid var(--border, #f3f4f6)' : 'none',
                                fontWeight: tz.id === timeZoneId ? 600 : 400
                              }}
                            >
                              {tz.label}
                              {tz.id === timeZoneId && (
                                <span style={{ float: 'right', color: '#047857' }}><Check size={16} color="currentColor" /></span>
                              )}
                            </li>
                          ))
                        )}
                      </ul>
                    )}
                  </div>

                  {timeZoneId && (
                    <div style={{
                      fontSize: '0.85rem',
                      color: 'var(--muted, #6b7280)',
                      marginBottom: '1rem',
                      marginTop: '-0.5rem'
                    }}>
                      Selected: <strong>{timeZoneId}</strong>
                      {utcOffsetMinutes !== '' && <span> — {formatOffset(Number(utcOffsetMinutes))}</span>}
                    </div>
                  )}

                  {message && (
                    <div className={`alert ${message.type === 'success' ? 'alert-success' : 'alert-error'}`}>
                      {message.text}
                    </div>
                  )}

                  <button
                    type="submit"
                    className="btn btn-primary"
                    disabled={saving}
                    style={{ marginTop: '0.75rem' }}
                  >
                    {saving ? 'Saving…' : 'Save Timezone'}
                  </button>
                </form>
              </div>
            </section>
          )}

          {/* Security Tab */}
          {activeTab === 'security' && (
            <section
              id="panel-security"
              role="tabpanel"
              aria-labelledby="tab-security"
              className="settings-panel"
            >
              <div className="settings-section">
                <h3 className="settings-section-title">
                  <ShieldCheck size={18} color="currentColor" style={{ verticalAlign: 'middle', marginRight: 6 }} /> Password & Authentication
                </h3>
                <p className="settings-section-desc">
                  Manage your password and authentication methods.
                </p>

                <div className="settings-security-item">
                  <div className="settings-security-info">
                    <strong>Password</strong>
                    <span>Last changed 30 days ago</span>
                  </div>
                  <button type="button" className="btn btn-secondary" style={{ fontSize: '0.85rem', padding: '6px 14px' }}>
                    Change Password
                  </button>
                </div>

                <div className="settings-security-item">
                  <div className="settings-security-info">
                    <strong>Two-Factor Authentication</strong>
                    <span className="settings-status settings-status-inactive">Not enabled</span>
                  </div>
                  <button type="button" className="btn btn-secondary" style={{ fontSize: '0.85rem', padding: '6px 14px' }}>
                    Enable 2FA
                  </button>
                </div>

                <div className="settings-security-item">
                  <div className="settings-security-info">
                    <strong>Active Sessions</strong>
                    <span>1 active session</span>
                  </div>
                  <button type="button" className="btn btn-secondary" style={{ fontSize: '0.85rem', padding: '6px 14px' }}>
                    Manage Sessions
                  </button>
                </div>
              </div>
            </section>
          )}

          {/* Notifications Tab */}
          {activeTab === 'notifications' && (
            <section
              id="panel-notifications"
              role="tabpanel"
              aria-labelledby="tab-notifications"
              className="settings-panel"
            >
              <NotificationPreferences />
            </section>
          )}

          {/* API Access Tab */}
          {activeTab === 'api-access' && (
            <section
              id="panel-api-access"
              role="tabpanel"
              aria-labelledby="tab-api-access"
              className="settings-panel"
            >
              <div className="settings-section">
                <h3 className="settings-section-title">
                  <KeyRound size={18} color="currentColor" style={{ verticalAlign: 'middle', marginRight: 6 }} /> API Keys & Access
                </h3>
                <p className="settings-section-desc">
                  Manage API keys for programmatic access to your account.
                </p>

                <div className="settings-api-warning">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/>
                    <line x1="12" y1="9" x2="12" y2="13"/>
                    <line x1="12" y1="17" x2="12.01" y2="17"/>
                  </svg>
                  <div>
                    <strong>Keep your API keys secure</strong>
                    <p>Never share API keys in public repositories or client-side code.</p>
                  </div>
                </div>

                <div className="settings-empty-state">
                  <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ opacity: 0.4 }}>
                    <polyline points="16 18 22 12 16 6"/>
                    <polyline points="8 6 2 12 8 18"/>
                  </svg>
                  <h4>No API keys</h4>
                  <p>Create an API key to integrate with external services and automation tools.</p>
                  <button type="button" className="btn btn-primary" style={{ marginTop: '1rem' }}>
                    Generate API Key
                  </button>
                </div>
              </div>
            </section>
          )}
        </div>
      </div>
    </div>
  );
};

/** Pill-shaped toggle switch */
const ToggleSwitch: React.FC<{
  checked: boolean;
  onChange: (checked: boolean) => void;
  activeColor: string;
  disabled?: boolean;
  ariaLabel: string;
}> = ({ checked, onChange, activeColor, disabled, ariaLabel }) => {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={ariaLabel}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      style={{
        position: 'relative',
        width: 44,
        height: 24,
        borderRadius: 999,
        border: 'none',
        cursor: disabled ? 'not-allowed' : 'pointer',
        background: checked ? activeColor : 'var(--border)',
        transition: 'background 0.2s ease',
        opacity: disabled ? 0.6 : 1,
        padding: 0,
        flexShrink: 0
      }}
    >
      <span
        style={{
          position: 'absolute',
          top: 2,
          left: checked ? 22 : 2,
          width: 20,
          height: 20,
          borderRadius: '50%',
          background: '#fff',
          boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
          transition: 'left 0.2s ease'
        }}
      />
    </button>
  );
};

const NotificationPreferences: React.FC = () => {
  const toast = useToast();
  const [updatingEvent, setUpdatingEvent] = React.useState<string | null>(null);
  const [localPrefs, setLocalPrefs] = React.useState<NotificationPreference[]>([]);

  const { data, loading, error, refetch } = useAsync<NotificationPreference[]>(
    async () => fetchPreferences(),
    []
  );

  React.useEffect(() => {
    if (data) {
      setLocalPrefs(data);
    }
  }, [data]);

  const handleToggle = async (eventType: string, field: 'emailEnabled' | 'smsEnabled', value: boolean) => {
    const pref = localPrefs.find(p => p.eventType === eventType);
    if (!pref) return;

    const newPref = { ...pref, [field]: value };
    const updatedPrefs = localPrefs.map(p => p.eventType === eventType ? newPref : p);

    // Optimistic update
    setLocalPrefs(updatedPrefs);
    setUpdatingEvent(eventType);

    try {
      await savePreferences(updatedPrefs);
      toast.success(`${formatEventName(eventType)} preference updated`);
    } catch (err: any) {
      // Rollback on error
      setLocalPrefs(prev => prev.map(p => p.eventType === eventType ? pref : p));
      toast.error(err.message || 'Failed to update preference');
    } finally {
      setUpdatingEvent(null);
    }
  };

  const formatEventName = (eventType: string): string => {
    return eventType
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, s => s.toUpperCase())
      .trim();
  };

  return (
    <div className="settings-section">
      <h3 className="settings-section-title">
        <Bell size={18} color="currentColor" style={{ verticalAlign: 'middle', marginRight: 6 }} /> Notification Preferences
      </h3>
      <p className="settings-section-desc">
        Control how you receive notifications for different events.
      </p>

      {loading && <PageLoader />}

      {error && (
        <div className="alert alert-error" style={{ marginBottom: '1rem' }}>
          {error}
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => refetch()}
            style={{ marginLeft: '1rem', padding: '4px 12px', fontSize: '0.8rem' }}
          >
            Retry
          </button>
        </div>
      )}

      {!loading && !error && localPrefs.length === 0 && (
        <p style={{ color: 'var(--muted)', fontSize: '0.9rem' }}>
          No notification preferences configured yet.
        </p>
      )}

      {!loading && !error && localPrefs.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table className="settings-notif-table">
            <thead>
              <tr>
                <th style={{ textAlign: 'left' }}>Event</th>
                <th style={{ textAlign: 'center', width: 80 }}>Email</th>
                <th style={{ textAlign: 'center', width: 80 }}>SMS</th>
              </tr>
            </thead>
            <tbody>
              {localPrefs.map(pref => (
                <tr key={pref.eventType}>
                  <td>{formatEventName(pref.eventType)}</td>
                  <td style={{ textAlign: 'center' }}>
                    <ToggleSwitch
                      checked={pref.emailEnabled}
                      onChange={(v) => handleToggle(pref.eventType, 'emailEnabled', v)}
                      activeColor="var(--primary)"
                      disabled={updatingEvent === pref.eventType}
                      ariaLabel={`Email notifications for ${formatEventName(pref.eventType)}`}
                    />
                  </td>
                  <td style={{ textAlign: 'center' }}>
                    <ToggleSwitch
                      checked={pref.smsEnabled}
                      onChange={(v) => handleToggle(pref.eventType, 'smsEnabled', v)}
                      activeColor="var(--accent)"
                      disabled={updatingEvent === pref.eventType}
                      ariaLabel={`SMS notifications for ${formatEventName(pref.eventType)}`}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

export default Settings;
