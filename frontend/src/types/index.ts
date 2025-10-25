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

// API Response types
export type ApiResponse<T> = T;
export type ApiError = {
  message: string;
  code?: string;
};
