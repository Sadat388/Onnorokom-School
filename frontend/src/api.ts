export const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:8080";

export type ClassInfo = {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
};

export type HoldStatus = "HELD" | "CONFIRMED" | "CANCELLED" | "EXPIRED";

export type Hold = {
  holdId: string;
  classId: string;
  className: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
};

export class ApiError extends Error {
  constructor(
    public code: string,
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

async function request<T>(
  path: string,
  init: RequestInit = {},
  studentId?: string,
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };
  if (studentId) headers["X-Student-Id"] = studentId;

  let res: Response;
  try {
    res = await fetch(`${API_URL}${path}`, { ...init, headers });
  } catch {
    throw new ApiError("NETWORK", "network error", 0);
  }
  if (res.status === 204) return undefined as T;

  const body = (await res.json().catch(() => ({}))) as {
    error?: string;
    message?: string;
  };
  if (!res.ok)
    throw new ApiError(body.error ?? "UNKNOWN", body.message ?? "", res.status);
  return body as T;
}

export const api = {
  listClasses: () => request<ClassInfo[]>("/classes"),
  createClass: (name: string, totalSeats: number) =>
    request<ClassInfo>("/classes", {
      method: "POST",
      body: JSON.stringify({ name, totalSeats }),
    }),
  applyHold: (classId: string, studentId: string) =>
    request<Hold>(`/classes/${classId}/holds`, { method: "POST" }, studentId),
  myHolds: (studentId: string) => request<Hold[]>("/me/holds", {}, studentId),
  confirm: (holdId: string, studentId: string) =>
    request<Hold>(`/holds/${holdId}/confirm`, { method: "POST" }, studentId),
  cancel: (holdId: string, studentId: string) =>
    request<void>(`/holds/${holdId}`, { method: "DELETE" }, studentId),
};
