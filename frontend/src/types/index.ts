// Core BCC API Types
export interface Group {
  group_id: number;
  label: string;
  parent_group_id: number | null;
  member_count: number;
  type?: string;
}

export interface GroupMember {
  name: string;
  person_uid: string;
  title?: string;
  link?: string;
  profile_picture?: string | null;
}

export interface PersonWithTitle {
  person_uid: string;
  name: string;
  profile_picture?: string | null;
}

// UI Component Types
export interface OrgNodeData {
  id: number | string;
  parentId: number | string | null;
  name: string;
  title: string;
  raw: Group;
  staffGroups?: Group[];
}

// Common Component Props
export interface BaseComponentProps {
  class?: string;
}

// Dialog related types
export interface DialogProps extends BaseComponentProps {
  title: string;
  description: string;
}

// Loading and error states
export interface AsyncState<T = any> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

// Roles / permissions
export type Role = "global_admin" | "local_admin" | "group_admin";

export interface RoleAssignment {
  role: Role;
  tenant_id: string | number | null;
  group_id: number | null;
}

export interface CurrentUser {
  email: string;
  name?: string;
  churchId?: number | string;
  roles: RoleAssignment[];
  [claim: string]: unknown;
}

// API Response types
export type ApiResponse<T> = T;
export type ApiError = {
  message: string;
  code?: string;
};
