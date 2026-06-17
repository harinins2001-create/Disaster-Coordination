"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  Alert,
  Badge,
  Button,
  Container,
  Group,
  Loader,
  Paper,
  Stack,
  Table,
  Text,
  Title,
} from "@mantine/core";
import SiteHeader from "@/components/SiteHeader";
import {
  cancelPledge,
  fetchMyAssignments,
  type Assignment,
} from "@/lib/api";

const statusColor = (s: string) => {
  switch (s) {
    case "active":
      return "green";
    case "done":
      return "blue";
    case "cancelled":
      return "gray";
    default:
      return "yellow";
  }
};

export default function MyAssignmentsPage() {
  const [items, setItems] = useState<Assignment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await fetchMyAssignments();
      setItems(list);
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to load assignments";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const onCancel = async (slug: string) => {
    if (!confirm("Cancel this volunteer pledge?")) return;
    setCancelling(slug);
    setError(null);
    try {
      await cancelPledge(slug);
      await load();
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Cancel failed";
      setError(msg);
    } finally {
      setCancelling(null);
    }
  };

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />
      <Container size="lg" py="xl">
        <Stack gap="lg">
          <Group justify="space-between" align="center">
            <Title order={2}>My volunteer pledges</Title>
            <Button component={Link} href="/profile" variant="subtle" size="sm">
              Back to profile
            </Button>
          </Group>

          {error && (
            <Alert color="red" variant="light">
              {error}
            </Alert>
          )}

          <Paper withBorder radius="md">
            {loading ? (
              <Group p="md">
                <Loader size="sm" />
                <Text>Loading…</Text>
              </Group>
            ) : items.length === 0 ? (
              <Text p="md" c="dimmed">
                You have no active pledges.
              </Text>
            ) : (
              <Table striped highlightOnHover verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Disaster</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Pledged at</Table.Th>
                    <Table.Th style={{ width: 160 }}>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {items.map((a) => (
                    <Table.Tr key={a.disasterSlug}>
                      <Table.Td>
                        <Link
                          href={`/disasters/${a.disasterSlug}`}
                          className="underline"
                        >
                          {a.disasterSlug}
                        </Link>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={statusColor(a.status)} variant="light">
                          {a.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" c="gray.7">
                          {a.createdAt ?? "—"}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        {a.status !== "cancelled" && a.status !== "done" && (
                          <Button
                            size="xs"
                            color="red"
                            variant="light"
                            loading={cancelling === a.disasterSlug}
                            onClick={() => onCancel(a.disasterSlug)}
                          >
                            Cancel
                          </Button>
                        )}
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            )}
          </Paper>
        </Stack>
      </Container>
    </div>
  );
}
