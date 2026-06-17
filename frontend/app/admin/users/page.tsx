"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  ActionIcon,
  Alert,
  Avatar,
  Badge,
  Button,
  Checkbox,
  Container,
  Group,
  Loader,
  Modal,
  MultiSelect,
  Paper,
  PasswordInput,
  Select,
  Stack,
  Switch,
  Table,
  Text,
  TextInput,
  Title,
} from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";
import { useForm } from "@mantine/form";
import { Pencil, Plus } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import { useHasRole } from "@/hooks/useProfile";
import {
  SKILL_OPTIONS,
  SRI_LANKAN_DISTRICTS,
  TRAVEL_OPTIONS,
  USER_ROLES,
  adminCreateUser,
  fetchUsers,
  setUserActive,
  setUserRoles,
  type SignupInput,
  type User,
} from "@/lib/api";

const NIC_RE = /^(\d{9}[VvXx]|\d{12})$/;
const PHONE_RE = /^\+?[0-9]{7,15}$/;

const toOptions = (arr: readonly string[]) =>
  arr.map((v) => ({ value: v, label: v.replace(/-/g, " ") }));

type InviteValues = SignupInput;

export default function AdminUsersPage() {
  const { hasRole: isAdmin, loading: profileLoading } = useHasRole("admin");

  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [roleFilter, setRoleFilter] = useState<string | null>(null);
  const [districtFilter, setDistrictFilter] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const [editing, setEditing] = useState<User | null>(null);
  const [rolesDraft, setRolesDraft] = useState<string[]>([]);
  const [rolesSaving, setRolesSaving] = useState(false);

  const [inviteOpened, { open: openInvite, close: closeInvite }] =
    useDisclosure(false);
  const [inviteSubmitting, setInviteSubmitting] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);

  const inviteForm = useForm<InviteValues>({
    initialValues: {
      email: "",
      password: "",
      name: "",
      nic: "",
      phone: "",
      dob: "",
      gender: "M",
      photoKey: "",
      area: "",
      skills: [],
      travelMethods: [],
      roles: [],
    },
    validate: {
      email: (v) => (/^\S+@\S+\.\S+$/.test(v) ? null : "Valid email required"),
      password: (v) => (v.length >= 8 ? null : "Min 8 characters"),
      name: (v) => (v.trim().length >= 2 ? null : "Name is required"),
      nic: (v) => (NIC_RE.test(v) ? null : "Invalid NIC"),
      phone: (v) => (PHONE_RE.test(v) ? null : "Invalid phone"),
      dob: (v) => (v.length === 10 ? null : "DOB required"),
      area: (v) => (v ? null : "District required"),
      roles: (v) => (v.length > 0 ? null : "Pick at least one role"),
    },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await fetchUsers({
        role: roleFilter ?? undefined,
        district: districtFilter ?? undefined,
        search: search || undefined,
      });
      setUsers(list);
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to load users";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [roleFilter, districtFilter, search]);

  useEffect(() => {
    if (isAdmin) load();
  }, [isAdmin, load]);

  const openRoles = (u: User) => {
    setEditing(u);
    setRolesDraft([...u.roles]);
  };

  const saveRoles = async () => {
    if (!editing) return;
    setRolesSaving(true);
    try {
      await setUserRoles(editing.sub, rolesDraft);
      setEditing(null);
      await load();
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to save roles";
      alert(msg);
    } finally {
      setRolesSaving(false);
    }
  };

  const toggleActive = async (u: User) => {
    const nextActive = !u.active;
    try {
      await setUserActive(u.sub, nextActive);
      await load();
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to toggle user";
      alert(msg);
    }
  };

  const onInvite = inviteForm.onSubmit(async (values) => {
    setInviteSubmitting(true);
    setInviteError(null);
    try {
      await adminCreateUser(values);
      closeInvite();
      inviteForm.reset();
      await load();
    } catch (err) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ?? "Failed to create user";
      setInviteError(msg);
    } finally {
      setInviteSubmitting(false);
    }
  });

  const roleOptions = useMemo(
    () => USER_ROLES.map((r) => ({ value: r, label: r })),
    [],
  );
  const districtOptions = useMemo(
    () => SRI_LANKAN_DISTRICTS.map((d) => ({ value: d, label: d })),
    [],
  );

  if (profileLoading) {
    return (
      <div className="min-h-screen bg-white">
        <SiteHeader />
        <Container size="lg" py="xl">
          <Group>
            <Loader size="sm" />
            <Text>Loading…</Text>
          </Group>
        </Container>
      </div>
    );
  }

  if (!isAdmin) {
    return (
      <div className="min-h-screen bg-white">
        <SiteHeader />
        <Container size="sm" py="xl">
          <Alert color="red" variant="light" title="Forbidden">
            You need the <b>admin</b> role to access this page.
          </Alert>
          <Group mt="md">
            <Button component={Link} href="/dashboard" variant="light">
              Back to dashboard
            </Button>
          </Group>
        </Container>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-white">
      <SiteHeader />
      <Container size="lg" py="xl">
        <Stack gap="lg">
          <Group justify="space-between">
            <Title order={2}>Users</Title>
            <Button leftSection={<Plus size={16} />} onClick={openInvite}>
              New user
            </Button>
          </Group>

          <Paper withBorder radius="md" p="md">
            <Group grow>
              <Select
                label="Role"
                placeholder="Any role"
                clearable
                data={roleOptions}
                value={roleFilter}
                onChange={setRoleFilter}
              />
              <Select
                label="District"
                placeholder="Any district"
                clearable
                searchable
                data={districtOptions}
                value={districtFilter}
                onChange={setDistrictFilter}
              />
              <TextInput
                label="Search"
                placeholder="Name, email or NIC"
                value={search}
                onChange={(e) => setSearch(e.currentTarget.value)}
              />
            </Group>
            <Group mt="sm" justify="flex-end">
              <Button variant="light" onClick={load} loading={loading}>
                Apply filters
              </Button>
            </Group>
          </Paper>

          {error && (
            <Alert color="red" variant="light">
              {error}
            </Alert>
          )}

          <Paper withBorder radius="md">
            {loading ? (
              <Group p="md">
                <Loader size="sm" />
                <Text>Loading users…</Text>
              </Group>
            ) : users.length === 0 ? (
              <Text p="md" c="dimmed">
                No users found.
              </Text>
            ) : (
              <Table striped highlightOnHover verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th style={{ width: 56 }}></Table.Th>
                    <Table.Th>Name / Email</Table.Th>
                    <Table.Th>NIC</Table.Th>
                    <Table.Th>District</Table.Th>
                    <Table.Th>Roles</Table.Th>
                    <Table.Th>Active</Table.Th>
                    <Table.Th style={{ width: 60 }}></Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {users.map((u) => (
                    <Table.Tr key={u.sub}>
                      <Table.Td>
                        <Avatar
                          src={u.photoUrl ?? undefined}
                          alt={u.name}
                          size="md"
                          radius="xl"
                          color="blue"
                        >
                          {(u.name ?? "?").charAt(0).toUpperCase()}
                        </Avatar>
                      </Table.Td>
                      <Table.Td>
                        <Text fw={500}>{u.name}</Text>
                        <Text size="xs" c="gray.7">
                          {u.email}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{u.nic}</Text>
                      </Table.Td>
                      <Table.Td>{u.area || "—"}</Table.Td>
                      <Table.Td>
                        <Group gap={4}>
                          {u.roles.map((r) => (
                            <Badge key={r} variant="light">
                              {r}
                            </Badge>
                          ))}
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Switch
                          checked={u.active}
                          onChange={() => toggleActive(u)}
                        />
                      </Table.Td>
                      <Table.Td>
                        <ActionIcon
                          variant="subtle"
                          aria-label="Edit roles"
                          onClick={() => openRoles(u)}
                        >
                          <Pencil size={16} />
                        </ActionIcon>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            )}
          </Paper>
        </Stack>
      </Container>

      <Modal
        opened={editing !== null}
        onClose={() => setEditing(null)}
        title={editing ? `Roles: ${editing.name}` : "Roles"}
        centered
      >
        {editing && (
          <Stack gap="sm">
            <Checkbox.Group value={rolesDraft} onChange={setRolesDraft}>
              <Stack gap="xs">
                {USER_ROLES.map((r) => (
                  <Checkbox key={r} value={r} label={r} />
                ))}
              </Stack>
            </Checkbox.Group>
            <Group justify="flex-end">
              <Button variant="subtle" onClick={() => setEditing(null)}>
                Cancel
              </Button>
              <Button onClick={saveRoles} loading={rolesSaving}>
                Save
              </Button>
            </Group>
          </Stack>
        )}
      </Modal>

      <Modal
        opened={inviteOpened}
        onClose={closeInvite}
        title="Create user"
        size="lg"
        centered
      >
        <form onSubmit={onInvite}>
          <Stack gap="sm">
            {inviteError && (
              <Alert color="red" variant="light">
                {inviteError}
              </Alert>
            )}
            <Group grow>
              <TextInput
                label="Email"
                required
                {...inviteForm.getInputProps("email")}
              />
              <TextInput
                label="Full name"
                required
                {...inviteForm.getInputProps("name")}
              />
            </Group>
            <PasswordInput
              label="Initial password"
              required
              {...inviteForm.getInputProps("password")}
            />
            <Group grow>
              <TextInput
                label="NIC"
                required
                {...inviteForm.getInputProps("nic")}
              />
              <TextInput
                label="Phone"
                required
                {...inviteForm.getInputProps("phone")}
              />
            </Group>
            <Group grow>
              <TextInput
                type="date"
                label="DOB"
                required
                {...inviteForm.getInputProps("dob")}
              />
              <Select
                label="Gender"
                data={[
                  { value: "M", label: "Male" },
                  { value: "F", label: "Female" },
                ]}
                required
                {...inviteForm.getInputProps("gender")}
              />
            </Group>
            <Select
              label="District"
              data={districtOptions}
              searchable
              required
              {...inviteForm.getInputProps("area")}
            />
            <MultiSelect
              label="Skills"
              data={toOptions(SKILL_OPTIONS)}
              searchable
              clearable
              {...inviteForm.getInputProps("skills")}
            />
            <MultiSelect
              label="Travel methods"
              data={toOptions(TRAVEL_OPTIONS)}
              searchable
              clearable
              {...inviteForm.getInputProps("travelMethods")}
            />
            <Checkbox.Group
              label="Roles"
              required
              {...inviteForm.getInputProps("roles")}
            >
              <Group mt="xs">
                {USER_ROLES.map((r) => (
                  <Checkbox key={r} value={r} label={r} />
                ))}
              </Group>
            </Checkbox.Group>
            <Group justify="flex-end" mt="sm">
              <Button variant="subtle" onClick={closeInvite}>
                Cancel
              </Button>
              <Button type="submit" loading={inviteSubmitting}>
                Create
              </Button>
            </Group>
          </Stack>
        </form>
      </Modal>
    </div>
  );
}
