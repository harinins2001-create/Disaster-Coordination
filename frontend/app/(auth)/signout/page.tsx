"use client";

import { useEffect } from "react";
import { signOut } from "next-auth/react";
import { Loader, Stack, Text } from "@mantine/core";

export default function SignOutPage() {
  useEffect(() => {
    signOut({ redirect: true, callbackUrl: "/signin" });
  }, []);

  return (
    <div className="flex min-h-screen items-center justify-center">
      <Stack align="center" gap="md">
        <Loader />
        <Text>Signing you out…</Text>
      </Stack>
    </div>
  );
}
