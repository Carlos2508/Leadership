export interface GateStudent {
  id: string; name: string; code: string; placement: string; year: string; hasPhoto: boolean;
}
export interface ScanResult {
  outcome: string; direction: string | null; at: string | null; message: string;
  student: GateStudent | null; eventId: string | null;
}
export interface GateEvent {
  id: string; studentId: string; studentName: string; studentCode: string; direction: string;
  occurredAt: string; source: string; recordedBy: string; note: string; voided: boolean; voidReason: string | null;
}
export interface DailyRow {
  studentId: string; name: string; code: string; placement: string;
  firstIn: string | null; lastOut: string | null; state: string; marks: number;
}
export interface DailyReport {
  date: string; expected: number; arrived: number; inside: number; left: number; absent: number; rows: DailyRow[];
}
export interface GateDevice {
  id: string; name: string; createdAt: string; lastSeenAt: string | null; revoked: boolean;
}
export interface MyGateDay { date: string; firstIn: string | null; lastOut: string | null; marks: number; }
export const gateStates: Record<string, string> = { Inside: 'Dentro', Left: 'Salió', Absent: 'Sin marcas' };
export const directionLabel = (value: string): string => value === 'In' ? 'Entrada' : 'Salida';
