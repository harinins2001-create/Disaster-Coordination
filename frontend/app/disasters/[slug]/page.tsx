"use client";

import { use, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import Image from "next/image";
import { useSession } from "next-auth/react";
import {
  ActionIcon,
  Alert,
  Avatar,
  Badge,
  Button,
  Container,
  Group,
  Loader,
  Modal,
  NumberInput,
  Paper,
  Progress,
  Select,
  Stack,
  Table,
  Text,
  Textarea,
  Title,
} from "@mantine/core";
import { useForm } from "@mantine/form";
import { useDisclosure } from "@mantine/hooks";
import { ArrowLeft, HandHeart, Minus, Plus } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import ResourcesChart, { mergeWithAllTypes } from "@/components/ResourcesChart";
import { useProfile } from "@/hooks/useProfile";
import {
  ITEM_TYPES,
  ITEM_TYPE_LABELS,
  cancelPledge,
  createDonation,
  fetchAssignments,
  fetchDisasterBySlug,
  fetchDisasterBySlugPublic,
  fetchDonations,
  fetchResources,
  pledgeVolunteer,
  upsertResource,
  type Assignment,
  type Disaster,
  type Donation,
  type ItemType,
  type Resource,
} from "@/lib/api";

const ITEM_TYPE_OPTIONS = ITEM_TYPES.map((t) => ({
  value: t,
  label: ITEM_TYPE_LABELS[t as ItemType],
}));

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

const statusColor = (status: string) => {
  switch (status?.toLowerCase()) {
    case "needs-met":
      return "green";
    case "active":
      return "red";
    case "monitoring":
      return "yellow";
    default:
      return "gray";
  }
};

export default function DisasterDetailPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = use(params);
  const { status: sessionStatus } = useSession();
  const { profile } = useProfile();
  const authed = sessionStatus === "authenticated";

  const [disaster, setDisaster] = useState<Disaster | null>(null);
  const [resources, setResources] = useState<Resource[]>([]);
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [donations, setDonations] = useState<Donation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState<Record<string, boolean>>({});
  const [pledging, setPledging] = useState(false);

  const [donateOpened, { open: openDonate, close: closeDonate }] =
    useDisclosure(false);
  const [donateSubmitting, setDonateSubmitting] = useState(false);
  const [donateError, setDonateError] = useState<string | null>(null);

  const donateForm = useForm<{
    itemType: string;
    quantity: number;
    note: string;
  }>({
    initialValues: { itemType: "food", quantity: 1, note: "" },
    validate: {
      itemType: (v) => (v ? null : "Item type required"),
      quantity: (v) => (v > 0 ? null : "Must be positive"),
    },
  });

  const load = useCallback(async () => {
    if (sessionStatus === "loading") return;
    setLoading(true);
    setError(null);
    try {
      const disasterP = authed
        ? fetchDisasterBySlug(slug).catch(() => null)
        : fetchDisasterBySlugPublic(slug).catch(() => null);
      const resP = fetchResources(slug).catch(() => [] as Resource[]);
      const asgnP = authed
        ? fetchAssignments(slug).catch(() => [] as Assignment[])
        : Promise.resolve([] as Assignment[]);
      const donP = authed
        ? fetchDonations(slug).catch(() => [] as Donation[])
        : Promise.resolve([] as Donation[]);

      const [d, res, asgns, dons] = await Promise.all([
        disasterP,
        resP,
        asgnP,
        donP,
      ]);
      setDisaster(d ?? null);
      setResources(res);
      setAssignments(asgns);
      setDonations(dons);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }, [slug, authed, sessionStatus]);

  useEffect(() => {
    load();
  }, [load]);

  const rows = useMemo(() => mergeWithAllTypes(resources), [resources]);

  const activeVolunteers = useMemo(
    () => assignments.filter((a) => a.status !== "cancelled").length,
    [assignments],
  );

  const myPledge = useMemo(
    () =>
      profile
        ? assignments.find(
            (a) => a.userSub === profile.sub && a.status !== "cancelled",
          )
        : undefined,
    [assignments, profile],
  );

  const canManageResources =
    !!profile &&
    profile.active &&
    (profile.roles.includes("admin") || profile.roles.includes("moderator"));

  const canVolunteer =
    !!profile &&
    profile.active &&
    (profile.roles.includes("helper") || profile.roles.includes("medic"));

  const canDonate = !!profile && profile.active;

  const setQuantity = async (itemType: ItemType, quantity: number) => {
    const clamped = Math.max(0, Math.floor(quantity));
    setPending((p) => ({ ...p, [itemType]: true }));
    setError(null);
    setResources((prev) => {
      const idx = prev.findIndex((r) => r.itemType === itemType);
      if (idx >= 0) {
        const copy = [...prev];
        copy[idx] = { ...copy[idx], quantity: clamped };
        return copy;
      }
      return [...prev, { disasterSlug: slug, itemType, quantity: clamped }];
    });
    try {
      await upsertResource(slug, itemType, clamped);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
      await load();
    } finally {
      setPending((p) => ({ ...p, [itemType]: false }));
    }
  };

  const bump = (itemType: ItemType, delta: number) => {
    const current = rows.find((r) => r.itemType === itemType)?.quantity ?? 0;
    setQuantity(itemType, current + delta);
  };

  const onPledge = async () => {
    setPledging(true);
    setError(null);
    try {
      await pledgeVolunteer(slug);
      await load();
    } catch (e) {
      const msg =
        (e as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Pledge failed";
      setError(msg);
    } finally {
      setPledging(false);
    }
  };

  const onCancelPledge = async () => {
    if (!confirm("Cancel your volunteer pledge?")) return;
    setPledging(true);
    setError(null);
    try {
      await cancelPledge(slug);
      await load();
    } catch (e) {
      const msg =
        (e as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Cancel failed";
      setError(msg);
    } finally {
      setPledging(false);
    }
  };

  const onDonate = donateForm.onSubmit(async (values) => {
    setDonateSubmitting(true);
    setDonateError(null);
    try {
      await createDonation({
        disasterSlug: slug,
        itemType: values.itemType,
        quantity: values.quantity,
        note: values.note,
      });
      closeDonate();
      donateForm.reset();
      await load();
    } catch (e) {
      const msg =
        (e as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Donation failed";
      setDonateError(msg);
    } finally {
      setDonateSubmitting(false);
    }
  });

  const resourcesByType = useMemo(() => {
    const map = new Map<string, number>();
    for (const r of resources) {
      map.set(r.itemType.toLowerCase(), r.quantity);
    }
    return map;
  }, [resources]);

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />

      <Container size="lg" py="xl">
        <Stack gap="lg">
          <Group gap="xs">
            <Button
              component={Link}
              href={authed ? "/dashboard" : "/"}
              variant="subtle"
              size="xs"
              leftSection={<ArrowLeft size={14} />}
            >
              {authed ? "Back to dashboard" : "Back to home"}
            </Button>
          </Group>

          {loading ? (
            <Group>
              <Loader size="sm" />
              <Text>Loading…</Text>
            </Group>
          ) : disaster ? (
            <>
              <div>
                <Group gap="sm" align="center">
                  <Title order={2}>{disaster.title}</Title>
                  <Badge color={severityColor(disaster.severity)} variant="light">
                    {disaster.severity}
                  </Badge>
                  <Badge color={statusColor(disaster.status)} variant="filled">
                    {disaster.status}
                  </Badge>
                </Group>
                <Text c="gray.7" size="sm" mt={4}>
                  {disaster.location || "—"} · {disaster.description}
                </Text>
                <Text c="dimmed" size="xs" mt={2}>
                  Reported by {disaster.reportedByName ?? disaster.reportedBy ?? "—"}
                </Text>
              </div>

              {disaster.status === "pending" && (
                <Alert color="yellow" variant="light" title="Pending review">
                  This submission is awaiting moderator approval. It is not
                  visible to the public yet.
                </Alert>
              )}

              {disaster.status === "rejected" && (
                <Alert color="red" variant="light" title="Rejected">
                  {disaster.rejectionReason ||
                    "This submission was rejected. You may revise and submit a new report."}
                </Alert>
              )}

              {disaster.photoUrls && disaster.photoUrls.length > 0 && (
                <Paper withBorder radius="md" p="md" shadow="xs">
                  <Title order={4} mb="sm">
                    Photos
                  </Title>
                  <Group gap="xs" wrap="wrap">
                    {disaster.photoUrls.map((url, idx) => (
                      <div
                        key={idx}
                        style={{
                          position: "relative",
                          width: 200,
                          height: 150,
                          borderRadius: 8,
                          overflow: "hidden",
                          border: "1px solid #e9ecef",
                        }}
                      >
                        <Image
                          src={url}
                          alt={`${disaster.title} photo ${idx + 1}`}
                          fill
                          sizes="200px"
                          style={{ objectFit: "cover" }}
                          unoptimized
                        />
                      </div>
                    ))}
                  </Group>
                </Paper>
              )}

              {error && (
                <Alert color="red" variant="light" title="Error">
                  {error}
                </Alert>
              )}

              <Paper withBorder radius="md" p="md" shadow="xs">
                <Group justify="space-between" align="center">
                  <div>
                    <Title order={4}>Volunteers</Title>
                    <Text size="sm" c="gray.7" mt={2}>
                      {activeVolunteers} of{" "}
                      {disaster.requiredVolunteers ?? 0} pledged
                    </Text>
                  </div>
                  {canVolunteer && (
                    <>
                      {myPledge ? (
                        <Button
                          color="red"
                          variant="light"
                          leftSection={<HandHeart size={16} />}
                          onClick={onCancelPledge}
                          loading={pledging}
                        >
                          Cancel pledge
                        </Button>
                      ) : (
                        <Button
                          color="green"
                          leftSection={<HandHeart size={16} />}
                          onClick={onPledge}
                          loading={pledging}
                        >
                          Volunteer
                        </Button>
                      )}
                    </>
                  )}
                </Group>
                {disaster.requiredVolunteers ? (
                  <Progress
                    mt="md"
                    value={Math.min(
                      100,
                      (activeVolunteers /
                        Math.max(1, disaster.requiredVolunteers)) *
                        100,
                    )}
                    color={
                      activeVolunteers >= disaster.requiredVolunteers
                        ? "green"
                        : "blue"
                    }
                  />
                ) : null}
              </Paper>

              {disaster.requiredResources &&
                disaster.requiredResources.length > 0 && (
                  <Paper withBorder radius="md" p="md" shadow="xs">
                    <Title order={4} mb="sm">
                      Required items
                    </Title>
                    <Table>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Item</Table.Th>
                          <Table.Th>Needed</Table.Th>
                          <Table.Th>On hand</Table.Th>
                          <Table.Th>Progress</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {disaster.requiredResources.map((req) => {
                          const have =
                            resourcesByType.get(req.itemType.toLowerCase()) ?? 0;
                          const pct = Math.min(
                            100,
                            (have / Math.max(1, req.quantity)) * 100,
                          );
                          return (
                            <Table.Tr key={req.itemType}>
                              <Table.Td>
                                {ITEM_TYPE_LABELS[req.itemType as ItemType] ??
                                  req.itemType}
                              </Table.Td>
                              <Table.Td>{req.quantity}</Table.Td>
                              <Table.Td>{have}</Table.Td>
                              <Table.Td>
                                <Progress
                                  value={pct}
                                  color={have >= req.quantity ? "green" : "blue"}
                                />
                              </Table.Td>
                            </Table.Tr>
                          );
                        })}
                      </Table.Tbody>
                    </Table>
                  </Paper>
                )}

              <Paper withBorder radius="md" p="md" shadow="xs">
                <Group justify="space-between" align="center" mb="sm">
                  <Title order={4}>Resources by item type</Title>
                  {canDonate && (
                    <Button
                      variant="light"
                      leftSection={<HandHeart size={16} />}
                      onClick={openDonate}
                    >
                      Donate
                    </Button>
                  )}
                </Group>
                <ResourcesChart resources={resources} height={280} />
              </Paper>

              {canManageResources && (
                <Paper withBorder radius="md" p="md" shadow="xs">
                  <Title order={4} mb="sm">
                    Adjust quantities
                  </Title>
                  <Stack gap="xs">
                    {ITEM_TYPES.map((t) => {
                      const current =
                        rows.find((r) => r.itemType === t)?.quantity ?? 0;
                      const busy = !!pending[t];
                      return (
                        <Group key={t} justify="space-between" wrap="nowrap">
                          <Text fw={500} style={{ minWidth: 120 }}>
                            {ITEM_TYPE_LABELS[t]}
                          </Text>
                          <Group gap="xs" wrap="nowrap">
                            <ActionIcon
                              variant="light"
                              color="red"
                              onClick={() => bump(t, -1)}
                              disabled={busy || current <= 0}
                              aria-label={`Decrement ${t}`}
                            >
                              <Minus size={16} />
                            </ActionIcon>
                            <NumberInput
                              value={current}
                              onChange={(v) =>
                                setQuantity(
                                  t,
                                  typeof v === "number" ? v : Number(v) || 0,
                                )
                              }
                              min={0}
                              w={96}
                              disabled={busy}
                              hideControls
                            />
                            <ActionIcon
                              variant="light"
                              color="green"
                              onClick={() => bump(t, 1)}
                              disabled={busy}
                              aria-label={`Increment ${t}`}
                            >
                              <Plus size={16} />
                            </ActionIcon>
                          </Group>
                        </Group>
                      );
                    })}
                  </Stack>
                </Paper>
              )}

              {authed && (
                <Paper withBorder radius="md" p="md" shadow="xs">
                  <Title order={4} mb="sm">
                    Recent donations
                  </Title>
                  {donations.length === 0 ? (
                    <Text c="dimmed" size="sm">
                      No donations recorded yet.
                    </Text>
                  ) : (
                    <Table striped verticalSpacing="sm">
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th>Contributor</Table.Th>
                          <Table.Th>Item</Table.Th>
                          <Table.Th>Quantity</Table.Th>
                          <Table.Th>Note</Table.Th>
                          <Table.Th>When</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {donations.map((d) => (
                          <Table.Tr key={d.id}>
                            <Table.Td>{d.userName}</Table.Td>
                            <Table.Td>
                              <Badge variant="light">
                                {ITEM_TYPE_LABELS[d.itemType as ItemType] ??
                                  d.itemType}
                              </Badge>
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
              )}

              {authed && (
                <Paper withBorder radius="md" p="md" shadow="xs">
                  <Title order={4} mb="sm">
                    Volunteers pledged
                  </Title>
                  {assignments.length === 0 ? (
                    <Text c="dimmed" size="sm">
                      No volunteers yet.
                    </Text>
                  ) : (
                    <Table striped verticalSpacing="sm">
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th style={{ width: 56 }}></Table.Th>
                          <Table.Th>Name</Table.Th>
                          <Table.Th>Email</Table.Th>
                          <Table.Th>Status</Table.Th>
                          <Table.Th>Pledged at</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {assignments.map((a) => (
                          <Table.Tr key={a.userSub}>
                            <Table.Td>
                              <Avatar
                                src={a.userPhotoUrl ?? undefined}
                                alt={a.userName}
                                size="md"
                                radius="xl"
                                color="blue"
                              >
                                {(a.userName ?? "?").charAt(0).toUpperCase()}
                              </Avatar>
                            </Table.Td>
                            <Table.Td>{a.userName}</Table.Td>
                            <Table.Td>{a.userEmail}</Table.Td>
                            <Table.Td>
                              <Badge variant="light">{a.status}</Badge>
                            </Table.Td>
                            <Table.Td>
                              <Text size="sm" c="gray.7">
                                {a.createdAt ?? "—"}
                              </Text>
                            </Table.Td>
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  )}
                </Paper>
              )}

              {!authed && (
                <Alert color="blue" variant="light">
                  <Text size="sm">
                    <Link href={`/signin?callbackUrl=${encodeURIComponent(`/disasters/${slug}`)}`} className="underline">
                      Sign in
                    </Link>{" "}
                    to volunteer, donate, or see the full contributor list.
                  </Text>
                </Alert>
              )}
            </>
          ) : (
            <Alert color="gray" variant="light">
              Disaster not found.
            </Alert>
          )}
        </Stack>
      </Container>

      <Modal
        opened={donateOpened}
        onClose={closeDonate}
        title="Record a donation"
        centered
      >
        <form onSubmit={onDonate}>
          <Stack gap="sm">
            {donateError && (
              <Alert color="red" variant="light">
                {donateError}
              </Alert>
            )}
            <Select
              label="Item type"
              data={ITEM_TYPE_OPTIONS}
              required
              {...donateForm.getInputProps("itemType")}
            />
            <NumberInput
              label="Quantity"
              min={1}
              required
              {...donateForm.getInputProps("quantity")}
            />
            <Textarea
              label="Note (optional)"
              autosize
              minRows={2}
              {...donateForm.getInputProps("note")}
            />
            <Group justify="flex-end" mt="sm">
              <Button variant="subtle" onClick={closeDonate}>
                Cancel
              </Button>
              <Button type="submit" loading={donateSubmitting}>
                Donate
              </Button>
            </Group>
          </Stack>
        </form>
      </Modal>
    </div>
  );
}
