"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  ActionIcon,
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
import { RotateCcw, Trash2 } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import {
  deleteDisaster,
  fetchMyDisasters,
  type Disaster,
} from "@/lib/api";

const statusColor = (status: string) => {
  switch (status?.toLowerCase()) {
    case "pending":
      return "yellow";
    case "rejected":
      return "red";
    case "active":
      return "green";
    case "needs-met":
      return "teal";
    case "resolved":
    case "closed":
      return "gray";
    default:
      return "blue";
  }
};

const severityColor = (severity: string) => {
  switch (severity?.toLowerCase()) {
    case "critical":
      return "red";
    case "high":
      return "orange";
    case "medium":
      return "yellow";
    case "low":
      return "blue";
    default:
      return "gray";
  }
};

export default function MySubmissionsPage() {
  const [items, setItems] = useState<Disaster[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await fetchMyDisasters();
      setItems(list);
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to load submissions";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const onDelete = async (d: Disaster) => {
    if (!confirm(`Delete "${d.title}"? This cannot be undone.`)) return;
    setError(null);
    try {
      await deleteDisaster(d.slug);
      await load();
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Delete failed";
      setError(msg);
    }
  };

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />
      <Container size="lg" py="xl">
        <Stack gap="lg">
          <Group justify="space-between" align="center">
            <Title order={2}>My submissions</Title>
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
                You have not submitted any disaster reports yet.
              </Text>
            ) : (
              <Table striped highlightOnHover verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Title</Table.Th>
                    <Table.Th>Severity</Table.Th>
                    <Table.Th>Location</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Rejection reason</Table.Th>
                    <Table.Th style={{ width: 140 }}>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {items.map((d) => (
                    <Table.Tr key={d.slug}>
                      <Table.Td>
                        <Link
                          href={`/disasters/${d.slug}`}
                          className="underline"
                        >
                          {d.title}
                        </Link>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={severityColor(d.severity)} variant="light">
                          {d.severity || "—"}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{d.location || "—"}</Table.Td>
                      <Table.Td>
                        <Badge color={statusColor(d.status)} variant="light">
                          {d.status || "—"}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" c="gray.7" lineClamp={2}>
                          {d.rejectionReason || "—"}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          {d.status === "pending" && (
                            <ActionIcon
                              variant="subtle"
                              color="red"
                              onClick={() => onDelete(d)}
                              aria-label="Delete"
                            >
                              <Trash2 size={16} />
                            </ActionIcon>
                          )}
                          {d.status === "rejected" && (
                            <ActionIcon
                              component={Link}
                              href="/dashboard"
                              variant="subtle"
                              color="blue"
                              aria-label="Submit a new report"
                              title="Submit a new report"
                            >
                              <RotateCcw size={16} />
                            </ActionIcon>
                          )}
                        </Group>
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
