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
  ITEM_TYPE_LABELS,
  fetchMyDonations,
  type Donation,
  type ItemType,
} from "@/lib/api";

const labelFor = (t: string) =>
  ITEM_TYPE_LABELS[t as ItemType] ?? t;

export default function MyDonationsPage() {
  const [items, setItems] = useState<Donation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await fetchMyDonations();
      setItems(list);
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to load donations";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />
      <Container size="lg" py="xl">
        <Stack gap="lg">
          <Group justify="space-between" align="center">
            <Title order={2}>My donations</Title>
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
                You have not made any donations yet.
              </Text>
            ) : (
              <Table striped highlightOnHover verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Disaster</Table.Th>
                    <Table.Th>Item</Table.Th>
                    <Table.Th>Quantity</Table.Th>
                    <Table.Th>Note</Table.Th>
                    <Table.Th>When</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {items.map((d) => (
                    <Table.Tr key={d.id}>
                      <Table.Td>
                        <Link
                          href={`/disasters/${d.disasterSlug}`}
                          className="underline"
                        >
                          {d.disasterSlug}
                        </Link>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light">{labelFor(d.itemType)}</Badge>
                      </Table.Td>
                      <Table.Td>{d.quantity}</Table.Td>
                      <Table.Td>
                        <Text size="sm" lineClamp={2}>
                          {d.note || "—"}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" c="gray.7">
                          {d.createdAt ?? "—"}
                        </Text>
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
