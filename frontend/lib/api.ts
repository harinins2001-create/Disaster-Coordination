import axios from "axios";
import { getSession } from "next-auth/react";

export const publicApi = axios.create({
  baseURL: process.env.NEXT_PUBLIC_PUBLIC_API_URL,
  headers: { "Content-Type": "application/json" },
});

export const privateApi = axios.create({
  baseURL: process.env.NEXT_PUBLIC_PRIVATE_API_URL,
  headers: { "Content-Type": "application/json" },
});

privateApi.interceptors.request.use(async (config) => {
  const session = await getSession();
  const idToken = session?.tokenData?.idToken;
  if (idToken) {
    config.headers.Authorization = `Bearer ${idToken}`;
  }
  return config;
});

export type ApiResponse<T> = {
  success: boolean;
  message?: string;
  data: T;
  fields?: Record<string, string>;
};

// ---------------------------------------------------------------------------
// Disasters
// ---------------------------------------------------------------------------

export type RequiredResource = {
  itemType: string;
  quantity: number;
};

export type Disaster = {
  slug: string;
  title: string;
  description: string;
  severity: string;
  location: string;
  status: string;
  reportedBy: string;
  reportedBySub?: string;
  reportedByName?: string;
  requiredVolunteers?: number;
  requiredResources?: RequiredResource[];
  photoKeys?: string[];
  photoUrls?: string[];
  rejectionReason?: string;
  createdAt?: string | null;
  updatedAt?: string | null;
};

export type DisasterSubmitInput = {
  title: string;
  description: string;
  location: string;
  severity?: string;
  requiredVolunteers?: number;
  requiredResources?: RequiredResource[];
  photos: File[];
};

export type DisasterEditInput = {
  title?: string;
  description?: string;
  severity?: string;
  location?: string;
  status?: string;
  requiredVolunteers?: number;
  requiredResources?: RequiredResource[];
};

export async function fetchDisasters(): Promise<Disaster[]> {
  const { data } = await publicApi.get<ApiResponse<Disaster[]>>("/api/Disaster");
  return data.data ?? [];
}

export async function fetchDisasterBySlugPublic(slug: string): Promise<Disaster> {
  const { data } = await publicApi.get<ApiResponse<Disaster>>(
    `/api/Disaster/${encodeURIComponent(slug)}`,
  );
  return data.data;
}

export async function fetchDisasterBySlug(slug: string): Promise<Disaster> {
  const { data } = await privateApi.get<ApiResponse<Disaster>>(
    `/api/Disaster/${encodeURIComponent(slug)}`,
  );
  return data.data;
}

export async function fetchAllDisasters(): Promise<Disaster[]> {
  const { data } = await privateApi.get<ApiResponse<Disaster[]>>("/api/Disaster/all");
  return data.data ?? [];
}

export async function fetchPendingDisasters(): Promise<Disaster[]> {
  const { data } = await privateApi.get<ApiResponse<Disaster[]>>("/api/Disaster/pending");
  return data.data ?? [];
}

export async function fetchMyDisasters(): Promise<Disaster[]> {
  const { data } = await privateApi.get<ApiResponse<Disaster[]>>("/api/Disaster/mine");
  return data.data ?? [];
}

export async function submitDisaster(input: DisasterSubmitInput): Promise<Disaster> {
  const fd = new FormData();
  fd.append("title", input.title);
  fd.append("description", input.description);
  fd.append("location", input.location);
  fd.append("severity", input.severity ?? "");
  fd.append("requiredVolunteers", String(input.requiredVolunteers ?? 0));
  (input.requiredResources ?? []).forEach((r, idx) => {
    fd.append(`requiredResources[${idx}].itemType`, r.itemType);
    fd.append(`requiredResources[${idx}].quantity`, String(r.quantity));
  });
  for (const photo of input.photos) {
    fd.append("photos", photo, photo.name);
  }
  const { data } = await privateApi.post<ApiResponse<Disaster>>("/api/Disaster", fd, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return data.data;
}

export async function updateDisaster(
  slug: string,
  input: DisasterEditInput,
): Promise<Disaster> {
  const { data } = await privateApi.put<ApiResponse<Disaster>>(
    `/api/Disaster/${slug}`,
    input,
  );
  return data.data;
}

export async function deleteDisaster(slug: string): Promise<void> {
  await privateApi.delete(`/api/Disaster/${slug}`);
}

export async function approveDisaster(slug: string): Promise<Disaster> {
  const { data } = await privateApi.post<ApiResponse<Disaster>>(
    `/api/Disaster/${encodeURIComponent(slug)}/approve`,
  );
  return data.data;
}

export async function rejectDisaster(slug: string, reason: string): Promise<Disaster> {
  const { data } = await privateApi.post<ApiResponse<Disaster>>(
    `/api/Disaster/${encodeURIComponent(slug)}/reject`,
    { reason },
  );
  return data.data;
}

// ---------------------------------------------------------------------------
// Resources
// ---------------------------------------------------------------------------

export const ITEM_TYPES = [
  "food",
  "water",
  "clothes",
  "medical",
  "shelter",
  "tools",
  "equipment",
  "other",
] as const;

export type ItemType = (typeof ITEM_TYPES)[number];

export const ITEM_TYPE_LABELS: Record<ItemType, string> = {
  food: "Food",
  water: "Water",
  clothes: "Clothes",
  medical: "Medical",
  shelter: "Shelter",
  tools: "Tools",
  equipment: "Equipment",
  other: "Other",
};

export type Resource = {
  disasterSlug: string;
  itemType: string;
  quantity: number;
  createdAt?: string | null;
  updatedAt?: string | null;
};

export async function fetchResources(disasterSlug: string): Promise<Resource[]> {
  const { data } = await publicApi.get<ApiResponse<Resource[]>>(
    `/api/Resource?disasterSlug=${encodeURIComponent(disasterSlug)}`,
  );
  return data.data ?? [];
}

export async function upsertResource(
  disasterSlug: string,
  itemType: string,
  quantity: number,
): Promise<Resource> {
  const { data } = await privateApi.put<ApiResponse<Resource>>(
    `/api/Resource/${encodeURIComponent(disasterSlug)}/${encodeURIComponent(itemType)}`,
    { quantity },
  );
  return data.data;
}

export async function deleteResource(
  disasterSlug: string,
  itemType: string,
): Promise<void> {
  await privateApi.delete(
    `/api/Resource/${encodeURIComponent(disasterSlug)}/${encodeURIComponent(itemType)}`,
  );
}

// ---------------------------------------------------------------------------
// Users
// ---------------------------------------------------------------------------

export const USER_ROLES = ["admin", "moderator", "medic", "helper"] as const;
export type UserRole = (typeof USER_ROLES)[number];

export const SRI_LANKAN_DISTRICTS = [
  "Colombo", "Gampaha", "Kalutara",
  "Kandy", "Matale", "Nuwara Eliya",
  "Galle", "Matara", "Hambantota",
  "Jaffna", "Kilinochchi", "Mannar", "Vavuniya", "Mullaitivu",
  "Batticaloa", "Ampara", "Trincomalee",
  "Kurunegala", "Puttalam",
  "Anuradhapura", "Polonnaruwa",
  "Badulla", "Monaragala",
  "Ratnapura", "Kegalle",
] as const;

export const SKILL_OPTIONS = [
  "first-aid", "cpr", "nursing", "doctor", "paramedic", "mental-health",
  "search-rescue", "firefighting", "driving-heavy", "driving-light",
  "boat-operator", "swimming", "rope-work", "climbing",
  "translation-sinhala", "translation-tamil", "translation-english",
  "cooking", "water-purification", "construction", "electrical", "plumbing",
  "it-support", "radio-ham", "logistics", "photography", "social-media",
  "counselling", "childcare", "elder-care", "pet-rescue", "veterinary",
  "drone-pilot", "gis-mapping", "data-entry", "general-labour",
] as const;

export const TRAVEL_OPTIONS = [
  "walk", "bicycle", "motorbike", "car", "van", "truck",
  "bus", "boat", "own-4x4", "public-transport", "on-foot-offroad",
] as const;

export type User = {
  sub: string;
  email: string;
  name: string;
  nic: string;
  phone: string;
  dob: string;
  gender: string;
  photoKey: string;
  photoUrl?: string | null;
  area: string;
  skills: string[];
  travelMethods: string[];
  roles: string[];
  active: boolean;
  createdAt?: string | null;
  updatedAt?: string | null;
};

export type SignupInput = {
  email: string;
  password: string;
  name: string;
  nic: string;
  phone: string;
  dob: string;
  gender: "M" | "F";
  photoKey: string;
  area: string;
  skills: string[];
  travelMethods: string[];
  roles: string[];
  photo?: File | null;
};

export async function signupPublic(input: SignupInput): Promise<User> {
  const fd = new FormData();
  fd.append("email", input.email);
  fd.append("password", input.password);
  fd.append("name", input.name);
  fd.append("nic", input.nic);
  fd.append("phone", input.phone);
  fd.append("dob", input.dob);
  fd.append("gender", input.gender);
  fd.append("photoKey", input.photoKey ?? "");
  fd.append("area", input.area);
  for (const s of input.skills) fd.append("skills", s);
  for (const t of input.travelMethods) fd.append("travelMethods", t);
  for (const r of input.roles) fd.append("roles", r);
  if (input.photo) fd.append("photo", input.photo, input.photo.name);

  const { data } = await publicApi.post<ApiResponse<User>>("/api/Auth/signup", fd, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return data.data;
}

export async function checkExists(params: { email?: string; nic?: string }): Promise<{
  emailExists: boolean;
  nicExists: boolean;
}> {
  const q = new URLSearchParams();
  if (params.email) q.set("email", params.email);
  if (params.nic) q.set("nic", params.nic);
  const { data } = await publicApi.get<ApiResponse<{ emailExists: boolean; nicExists: boolean }>>(
    `/api/User/exists?${q.toString()}`,
  );
  return data.data;
}

export async function fetchMe(): Promise<User> {
  const { data } = await privateApi.get<ApiResponse<User>>("/api/User/me");
  return data.data;
}

export async function updateMe(patch: {
  photoKey?: string;
  area?: string;
  skills?: string[];
  travelMethods?: string[];
}): Promise<User> {
  const { data } = await privateApi.put<ApiResponse<User>>("/api/User/me", patch);
  return data.data;
}

export async function uploadMyPhoto(file: File): Promise<User> {
  const fd = new FormData();
  fd.append("photo", file, file.name);
  const { data } = await privateApi.post<ApiResponse<User>>("/api/User/me/photo", fd, {
    headers: { "Content-Type": "multipart/form-data" },
  });
  return data.data;
}

export async function fetchUsers(params?: {
  role?: string;
  district?: string;
  search?: string;
}): Promise<User[]> {
  const q = new URLSearchParams();
  if (params?.role) q.set("role", params.role);
  if (params?.district) q.set("district", params.district);
  if (params?.search) q.set("search", params.search);
  const { data } = await privateApi.get<ApiResponse<User[]>>(
    `/api/User?${q.toString()}`,
  );
  return data.data ?? [];
}

export async function adminCreateUser(input: SignupInput): Promise<User> {
  const { data } = await privateApi.post<ApiResponse<User>>("/api/User", input);
  return data.data;
}

export async function setUserRoles(sub: string, roles: string[]): Promise<User> {
  const { data } = await privateApi.put<ApiResponse<User>>(
    `/api/User/${encodeURIComponent(sub)}/roles`,
    { roles },
  );
  return data.data;
}

export async function setUserActive(sub: string, active: boolean): Promise<User> {
  const { data } = await privateApi.put<ApiResponse<User>>(
    `/api/User/${encodeURIComponent(sub)}/active`,
    { active },
  );
  return data.data;
}

// ---------------------------------------------------------------------------
// Assignments (volunteer pledges)
// ---------------------------------------------------------------------------

export type Assignment = {
  disasterSlug: string;
  userSub: string;
  userName: string;
  userEmail: string;
  userPhotoKey?: string;
  userPhotoUrl?: string | null;
  status: "pledged" | "active" | "done" | "cancelled" | string;
  createdAt?: string | null;
  updatedAt?: string | null;
};

export async function pledgeVolunteer(disasterSlug: string): Promise<Assignment> {
  const { data } = await privateApi.post<ApiResponse<Assignment>>(
    "/api/Assignment/pledge",
    { disasterSlug },
  );
  return data.data;
}

export async function cancelPledge(disasterSlug: string): Promise<void> {
  await privateApi.delete(`/api/Assignment/${encodeURIComponent(disasterSlug)}`);
}

export async function setAssignmentStatus(
  disasterSlug: string,
  userSub: string,
  status: "pledged" | "active" | "done" | "cancelled",
): Promise<Assignment> {
  const { data } = await privateApi.put<ApiResponse<Assignment>>(
    `/api/Assignment/${encodeURIComponent(disasterSlug)}/${encodeURIComponent(userSub)}/status`,
    { status },
  );
  return data.data;
}

export async function fetchMyAssignments(): Promise<Assignment[]> {
  const { data } = await privateApi.get<ApiResponse<Assignment[]>>("/api/Assignment/me");
  return data.data ?? [];
}

export async function fetchAssignments(disasterSlug: string): Promise<Assignment[]> {
  const { data } = await privateApi.get<ApiResponse<Assignment[]>>(
    `/api/Assignment?disasterSlug=${encodeURIComponent(disasterSlug)}`,
  );
  return data.data ?? [];
}

// ---------------------------------------------------------------------------
// Donations
// ---------------------------------------------------------------------------

export type Donation = {
  id: string;
  disasterSlug: string;
  userSub: string;
  userName: string;
  itemType: string;
  quantity: number;
  note: string;
  createdAt?: string | null;
  updatedAt?: string | null;
};

export async function createDonation(input: {
  disasterSlug: string;
  itemType: string;
  quantity: number;
  note?: string;
}): Promise<Donation> {
  const { data } = await privateApi.post<ApiResponse<Donation>>("/api/Donation", {
    note: "",
    ...input,
  });
  return data.data;
}

export async function fetchMyDonations(): Promise<Donation[]> {
  const { data } = await privateApi.get<ApiResponse<Donation[]>>("/api/Donation/me");
  return data.data ?? [];
}

export async function fetchDonations(disasterSlug: string): Promise<Donation[]> {
  const { data } = await privateApi.get<ApiResponse<Donation[]>>(
    `/api/Donation?disasterSlug=${encodeURIComponent(disasterSlug)}`,
  );
  return data.data ?? [];
}
