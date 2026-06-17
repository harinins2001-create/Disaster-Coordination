"use client";

import { useSession } from "next-auth/react";
import { Container, Stack, Text, Title } from "@mantine/core";
import SiteHeader from "@/components/SiteHeader";
import DisastersManager from "@/components/DisastersManager";

export default function AdminPage() {
  const { data: session } = useSession();

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />

      <Container size="lg" py={48} px="md">
        <Stack gap="xl">
          <Stack gap={4}>
            <Title order={2}>Admin — Disasters</Title>
            <Text c="gray.6" size="sm">
              Signed in as {session?.user?.email ?? "—"}
            </Text>
          </Stack>

          <DisastersManager />
        </Stack>
      </Container>
    </div>
  );
}
