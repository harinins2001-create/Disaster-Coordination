"use client";

import { useCallback, useEffect, useState } from "react";
import { useSession } from "next-auth/react";
import { fetchMe, type User } from "@/lib/api";

type ProfileState = {
  profile: User | null;
  loading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
};

export function useProfile(): ProfileState {
  const { status } = useSession();
  const [profile, setProfile] = useState<User | null>(null);
  const [loading, setLoading] = useState<boolean>(status === "authenticated");
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (status !== "authenticated") {
      setProfile(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const me = await fetchMe();
      setProfile(me);
    } catch (err) {
      const message =
        (err as { response?: { data?: { message?: string } }; message?: string })
          ?.response?.data?.message ??
        (err as { message?: string })?.message ??
        "Failed to load profile";
      setError(message);
      setProfile(null);
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => {
    if (status === "loading") return;
    refresh();
  }, [status, refresh]);

  return { profile, loading, error, refresh };
}

export function useHasRole(role: string | string[]): {
  hasRole: boolean;
  loading: boolean;
  profile: User | null;
} {
  const { profile, loading } = useProfile();
  const roles = Array.isArray(role) ? role : [role];
  const hasRole =
    !!profile &&
    profile.active &&
    profile.roles.some((r) => roles.includes(r.toLowerCase()));
  return { hasRole, loading, profile };
}
