import { useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { useApiClient } from '../hooks/useApiClient'

// These types mirror the NSwag-generated types produced after backend build
interface AddressDto {
  street: string
  houseNumber: string
  zipCode: string
  city: string
}

interface OrganizationMemberDto {
  userId: string
  username: string
  firstName?: string | null
  lastName?: string | null
  email: string
  isOrganisator: boolean
}

interface OrganizationDetailsResponse {
  id: string
  name: string
  description?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  website?: string | null
  address?: AddressDto | null
  createdOn: string
  modifiedOn?: string | null
  members: OrganizationMemberDto[]
}

type Tab = 'general' | 'members'

export default function OrganizationSettingsPage() {
  const { organizationId } = useParams<{ organizationId: string }>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const api = useApiClient() as any
  const [activeTab, setActiveTab] = useState<Tab>('general')
  const [org, setOrg] = useState<OrganizationDetailsResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  const [form, setForm] = useState({
    name: '',
    description: '',
    contactEmail: '',
    contactPhone: '',
    website: '',
    street: '',
    houseNumber: '',
    zipCode: '',
    city: '',
  })

  useEffect(() => {
    if (!organizationId) return
    setLoading(true)
    api.getOrganizationDetails(organizationId)
      .then((data: OrganizationDetailsResponse) => {
        setOrg(data)
        setForm({
          name: data.name,
          description: data.description ?? '',
          contactEmail: data.contactEmail ?? '',
          contactPhone: data.contactPhone ?? '',
          website: data.website ?? '',
          street: data.address?.street ?? '',
          houseNumber: data.address?.houseNumber ?? '',
          zipCode: data.address?.zipCode ?? '',
          city: data.address?.city ?? '',
        })
      })
      .catch(() => setError('Organisation konnte nicht geladen werden.'))
      .finally(() => setLoading(false))
  }, [organizationId])

  const hasAddress =
    form.street || form.houseNumber || form.zipCode || form.city

  async function handleSave(e: React.FormEvent) {
    e.preventDefault()
    if (!organizationId) return
    setSaving(true)
    setError(null)
    setSuccessMessage(null)
    try {
      await api.updateOrganization(organizationId, {
        name: form.name,
        description: form.description || null,
        contactEmail: form.contactEmail || null,
        contactPhone: form.contactPhone || null,
        website: form.website || null,
        address: hasAddress
          ? {
              street: form.street,
              houseNumber: form.houseNumber,
              zipCode: form.zipCode,
              city: form.city,
            }
          : null,
      })
      setSuccessMessage('Änderungen gespeichert.')
      setOrg(prev =>
        prev
          ? {
              ...prev,
              name: form.name,
              description: form.description || null,
              contactEmail: form.contactEmail || null,
              contactPhone: form.contactPhone || null,
              website: form.website || null,
              address: hasAddress
                ? {
                    street: form.street,
                    houseNumber: form.houseNumber,
                    zipCode: form.zipCode,
                    city: form.city,
                  }
                : null,
            }
          : prev,
      )
    } catch {
      setError('Speichern fehlgeschlagen.')
    } finally {
      setSaving(false)
    }
  }

  async function handleRemoveMember(userId: string) {
    if (!organizationId) return
    try {
      await api.removeMember(organizationId, userId)
      setOrg(prev =>
        prev
          ? { ...prev, members: prev.members.filter(m => m.userId !== userId) }
          : prev,
      )
    } catch {
      setError('Mitglied konnte nicht entfernt werden.')
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <span className="text-gray-500">Wird geladen…</span>
      </div>
    )
  }

  if (!org) {
    return (
      <div className="py-8 text-center text-red-600">
        Organisation nicht gefunden.
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-1 text-2xl font-bold text-gray-900">{org.name}</h1>
      <p className="mb-6 text-sm text-gray-500">
        Erstellt am{' '}
        {new Date(org.createdOn).toLocaleDateString('de-DE', {
          day: '2-digit',
          month: 'long',
          year: 'numeric',
        })}
      </p>

      <div className="mb-6 flex gap-4 border-b border-gray-200">
        {(['general', 'members'] as Tab[]).map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`pb-2 text-sm font-medium transition-colors ${
              activeTab === tab
                ? 'border-b-2 border-gray-900 text-gray-900'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab === 'general' ? 'Allgemein' : `Mitglieder (${org.members.length})`}
          </button>
        ))}
      </div>

      {error && (
        <div className="mb-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}
      {successMessage && (
        <div className="mb-4 rounded-md bg-green-50 px-4 py-3 text-sm text-green-700">
          {successMessage}
        </div>
      )}

      {activeTab === 'general' && (
        <form onSubmit={handleSave} className="space-y-5">
          <Field label="Name *">
            <input
              required
              value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
              className={inputClass}
            />
          </Field>

          <Field label="Beschreibung">
            <textarea
              rows={3}
              value={form.description}
              onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
              className={inputClass}
            />
          </Field>

          <Field label="Kontakt-E-Mail">
            <input
              type="email"
              value={form.contactEmail}
              onChange={e => setForm(f => ({ ...f, contactEmail: e.target.value }))}
              className={inputClass}
            />
          </Field>

          <Field label="Telefon">
            <input
              type="tel"
              value={form.contactPhone}
              onChange={e => setForm(f => ({ ...f, contactPhone: e.target.value }))}
              className={inputClass}
            />
          </Field>

          <Field label="Website">
            <input
              type="url"
              value={form.website}
              onChange={e => setForm(f => ({ ...f, website: e.target.value }))}
              placeholder="https://"
              className={inputClass}
            />
          </Field>

          <fieldset className="rounded-md border border-gray-200 p-4">
            <legend className="px-1 text-sm font-medium text-gray-700">
              Hauptadresse
            </legend>
            <div className="mt-3 grid grid-cols-3 gap-3">
              <div className="col-span-2">
                <label className={labelClass}>Straße</label>
                <input
                  value={form.street}
                  onChange={e => setForm(f => ({ ...f, street: e.target.value }))}
                  className={inputClass}
                />
              </div>
              <div>
                <label className={labelClass}>Hausnummer</label>
                <input
                  value={form.houseNumber}
                  onChange={e => setForm(f => ({ ...f, houseNumber: e.target.value }))}
                  className={inputClass}
                />
              </div>
              <div>
                <label className={labelClass}>PLZ</label>
                <input
                  maxLength={5}
                  value={form.zipCode}
                  onChange={e => setForm(f => ({ ...f, zipCode: e.target.value }))}
                  className={inputClass}
                />
              </div>
              <div className="col-span-2">
                <label className={labelClass}>Stadt</label>
                <input
                  value={form.city}
                  onChange={e => setForm(f => ({ ...f, city: e.target.value }))}
                  className={inputClass}
                />
              </div>
            </div>
          </fieldset>

          <div className="flex justify-end">
            <button
              type="submit"
              disabled={saving}
              className="rounded-md bg-gray-900 px-5 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50"
            >
              {saving ? 'Wird gespeichert…' : 'Speichern'}
            </button>
          </div>
        </form>
      )}

      {activeTab === 'members' && (
        <ul className="divide-y divide-gray-100">
          {org.members.map(member => (
            <li key={member.userId} className="flex items-center justify-between py-3">
              <div>
                <p className="text-sm font-medium text-gray-900">
                  {member.firstName && member.lastName
                    ? `${member.firstName} ${member.lastName}`
                    : member.username}
                </p>
                <p className="text-xs text-gray-500">{member.email}</p>
                {member.isOrganisator && (
                  <span className="mt-0.5 inline-block rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
                    Organisator
                  </span>
                )}
              </div>
              <button
                onClick={() => handleRemoveMember(member.userId)}
                className="text-xs text-red-500 hover:text-red-700"
              >
                Entfernen
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

const inputClass =
  'mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none'

const labelClass = 'block text-xs text-gray-600'

function Field({
  label,
  children,
}: {
  label: string
  children: React.ReactNode
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700">{label}</label>
      {children}
    </div>
  )
}
