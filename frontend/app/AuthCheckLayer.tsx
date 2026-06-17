"use client";

import { useEffect } from "react";
import { signOut, useSession } from "next-auth/react";
import { usePathname, useRouter } from "next/navigation";

// Routes that require authentication. Everything else is publicly visitable.
const PROTECTED_PREFIXES = ["/admin", "/dashboard", "/profile"];

export default function AuthCheckLayer({ children }: { children: React.ReactNode }) {
  const { data: session, status } = useSession();
  const pathname = usePathname();
  const router = useRouter();

  useEffect(() => {
    const isProtected = PROTECTED_PREFIXES.some((p) => pathname.startsWith(p));

    if (session?.error === "RefreshAccessTokenError") {
      signOut({ redirect: true, callbackUrl: "/signin" });
      return;
    }

    if (isProtected && status === "unauthenticated") {
      router.push(`/signin?callbackUrl=${encodeURIComponent(pathname)}`);
    }
  }, [session, status, pathname, router]);

  return <>{children}</>;
}
