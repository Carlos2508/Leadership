export interface ContactItem {
  id?: string;
  kind: 'Phone' | 'Email' | 'Address';
  value: string;
  label: string;
  isPrimary: boolean;
}
export interface PersonSummary {
  id: string;
  displayName: string;
  givenNames: string;
  familyNames: string;
  studentCode: string | null;
  isGuardian: boolean;
  status: 'Active' | 'Archived';
  account: string | null;
  isStaff: boolean;
}
export interface RelationshipItem {
  id: string;
  personId: string;
  displayName: string;
  studentCode: string | null;
  kind: string;
  startsOn: string;
  endedAt: string | null;
  endReason: string | null;
  faculties: string[];
  version: string;
}
export interface LinkedAccount {
  userId: string;
  username: string;
  status: 'Pending' | 'Active' | 'Suspended';
  linkedAt: string;
}
export interface PersonDetail {
  id: string;
  displayName: string;
  givenNames: string;
  familyNames: string;
  preferredName: string | null;
  birthDate: string | null;
  status: 'Active' | 'Archived';
  version: string;
  contacts: ContactItem[];
  studentCode: string | null;
  isGuardian: boolean;
  account: LinkedAccount | null;
  guardians: RelationshipItem[];
  students: RelationshipItem[];
  createdAt: string;
  staffPosition: string | null;
}
export interface FacultyDefinition {
  key: string;
  label: string;
  description: string;
}
export interface PeopleCatalog {
  relationshipKinds: string[];
  faculties: FacultyDefinition[];
}
export interface LinkableAccount {
  id: string;
  username: string;
  status: 'Pending' | 'Active' | 'Suspended';
}
export interface MyPlacement {
  year: string;
  grade: string;
  section: string | null;
  status: string;
}
export interface MyStudent {
  id: string;
  displayName: string;
  studentCode: string;
  relationship: string;
  faculties: string[];
  placement: MyPlacement | null;
}
export interface MyProfile {
  person: {
    id: string;
    displayName: string;
    studentCode: string | null;
    isGuardian: boolean;
    isStaff?: boolean;
  } | null;
  placement: MyPlacement | null;
  students: MyStudent[];
}

export const relationshipLabels: Record<string, string> = {
  Mother: 'Madre',
  Father: 'Padre',
  Grandparent: 'Abuela o abuelo',
  Sibling: 'Hermana o hermano',
  OtherRelative: 'Otro familiar',
  LegalGuardian: 'Tutela legal',
  Other: 'Otro vínculo',
  Self: 'Titular',
};
export const contactLabels: Record<string, string> = {
  Phone: 'Teléfono',
  Email: 'Correo',
  Address: 'Dirección',
};
export const facultyLabels: Record<string, string> = {
  AcademicRecords: 'Información académica',
  Finance: 'Estado de cuenta',
  BillingResponsible: 'Responsable de pago',
  EmergencyContact: 'Contacto de emergencia',
  Pickup: 'Autorizado para recoger',
  Communications: 'Comunicaciones',
};
export const accountStatusLabels: Record<string, string> = {
  Pending: 'Pendiente de activación',
  Active: 'Activa',
  Suspended: 'Suspendida',
};

/** Formats a calendar date (yyyy-MM-dd) without shifting it through the browser time zone. */
export function formatDateOnly(value: string | null, culture: string): string {
  if (!value) return '—';
  const [year, month, day] = value.split('-').map(Number);
  return new Intl.DateTimeFormat(culture, { dateStyle: 'medium', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, month - 1, day)),
  );
}
