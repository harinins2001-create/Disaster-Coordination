"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useSession, signOut } from "next-auth/react";
import {
  Avatar,
  Badge,
  Button,
  Divider,
  Group,
  Menu,
  Text,
  UnstyledButton,
} from "@mantine/core";
import { LifeBuoy, LogOut, ShieldCheck, User } from "lucide-react";
import { useProfile } from "@/hooks/useProfile";

type NavLink = { href: string; label: string; color?: string };

export default function SiteHeader() {
  const pathname = usePathname();
  const { data: session, status } = useSession();
  const { profile } = useProfile();
  const authed = status === "authenticated";
  const isAdmin =
    !!profile && profile.active && profile.roles.includes("admin");
  const isAdminOrMod =
    !!profile &&
    profile.active &&
    (profile.roles.includes("admin") || profile.roles.includes("moderator"));

  const displayName = profile?.name ?? session?.user?.email ?? "User";
  const initial = displayName.charAt(0).toUpperCase();
  const roleTag = isAdmin ? "admin" : isAdminOrMod ? "moderator" : null;

  const links: NavLink[] = [];
  if (authed) {
    links.push({ href: "/dashboard", label: "Dashboard" });
    if (isAdminOrMod) {
      links.push({
        href: "/admin/disasters/review",
        label: "Review",
        color: "orange",
      });
    }
    if (isAdmin) {
      links.push({ href: "/admin/users", label: "Admin", color: "grape" });
    }
  }

  const isActive = (href: string) =>
    pathname === href || pathname?.startsWith(`${href}/`);

  return (
    <header className="sticky top-0 z-40 border-b border-gray-200 bg-white/80 backdrop-blur supports-[backdrop-filter]:bg-white/70">
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-6 py-3">
        <Link href="/" className="flex items-center gap-2 no-underline">
          <span className="flex h-8 w-8 items-center justify-center rounded-md bg-blue-600 text-white">
            <LifeBuoy size={18} strokeWidth={2.2} />
          </span>
          <span className="flex flex-col leading-tight">
            <Text fw={700} size="md" className="tracking-tight">
              DRCS
            </Text>
            <Text size="xs" c="gray.6" className="hidden sm:inline">
              Disaster Response Coordination
            </Text>
          </span>
        </Link>

        <Group gap="xs" wrap="nowrap">
          {links.map((l) => (
            <Button
              key={l.href}
              component={Link}
              href={l.href}
              variant={isActive(l.href) ? "filled" : "subtle"}
              size="sm"
              color={l.color ?? "blue"}
              radius="md"
            >
              {l.label}
            </Button>
          ))}

          {authed ? (
            <Menu shadow="md" width={240} position="bottom-end" withArrow>
              <Menu.Target>
                <UnstyledButton
                  className="ml-1 flex items-center gap-2 rounded-full p-1 pr-3 hover:bg-gray-100"
                  aria-label="Open user menu"
                >
                  <Avatar
                    src={profile?.photoUrl ?? undefined}
                    alt={displayName}
                    size="sm"
                    radius="xl"
                    color="blue"
                  >
                    {initial}
                  </Avatar>
                  <Text size="sm" c="gray.8" className="hidden md:inline">
                    {displayName}
                  </Text>
                  {roleTag && (
                    <Badge
                      size="xs"
                      variant="light"
                      color={roleTag === "admin" ? "grape" : "orange"}
                      className="hidden md:inline-flex"
                    >
                      {roleTag}
                    </Badge>
                  )}
                </UnstyledButton>
              </Menu.Target>

              <Menu.Dropdown>
                <div className="px-3 pt-2 pb-2">
                  <Text size="sm" fw={600} truncate>
                    {displayName}
                  </Text>
                  <Text size="xs" c="gray.6" truncate>
                    {session?.user?.email}
                  </Text>
                  {roleTag && (
                    <Badge
                      mt={6}
                      size="xs"
                      variant="light"
                      color={roleTag === "admin" ? "grape" : "orange"}
                      leftSection={<ShieldCheck size={10} />}
                    >
                      {roleTag}
                    </Badge>
                  )}
                </div>
                <Divider />
                <Menu.Item
                  component={Link}
                  href="/profile"
                  leftSection={<User size={14} />}
                >
                  Profile
                </Menu.Item>
                <Menu.Divider />
                <Menu.Item
                  color="red"
                  leftSection={<LogOut size={14} />}
                  onClick={() => signOut({ callbackUrl: "/" })}
                >
                  Sign out
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          ) : (
            <>
              <Button
                component={Link}
                href="/signup"
                variant="subtle"
                size="sm"
                radius="md"
              >
                Sign up
              </Button>
              <Button
                component={Link}
                href="/signin"
                variant="filled"
                size="sm"
                radius="md"
              >
                Sign in
              </Button>
            </>
          )}
        </Group>
      </div>
    </header>
  );
}
