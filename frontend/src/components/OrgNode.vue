<template>
  <div class="relative w-full h-full">
    <div
      :class="[
        'box-border w-full h-full p-3 rounded-lg shadow-sm flex items-center hover:bg-neutral-50 transition-colors cursor-pointer',
        props.isExpanded
          ? 'border-2 border-brand-500 bg-brand-50'
          : 'border border-neutral-200 bg-neutral-0',
      ]"
      @click="handleNodeClick"
    >
      <div class="ml-3 min-w-0 flex-1">
        <div
          class="text-body-sm font-semibold text-neutral-900 whitespace-nowrap overflow-hidden text-ellipsis"
          :title="name"
        >
          {{ name }}
        </div>
        <div
          class="text-caption text-neutral-600 whitespace-nowrap overflow-hidden text-ellipsis"
          :title="title"
        >
          {{ title }}
        </div>
      </div>
      <div
        v-if="props.staffGroups && props.staffGroups.length > 0"
        class="flex-shrink-0 ml-2"
      >
        <div
          class="w-5 h-5 rounded-full bg-brand-500 text-neutral-0 flex items-center justify-center text-xs font-bold"
          :title="`${props.staffGroups.length} staber`"
        >
          {{ props.staffGroups.length }}
        </div>
      </div>
    </div>

    <button
      v-if="props.adminMode && props.canMoveLeft"
      class="absolute left-0 top-1/2 -translate-y-1/2 -translate-x-1/2 w-6 h-6 flex items-center justify-center rounded-full border border-brand-500 bg-brand-50 hover:bg-brand-100 shadow-sm z-10"
      title="Flytt til venstre"
      @click.stop="handleMoveLeft"
    >
      <Icon name="ChevronLeft" :size="12" class="text-brand-600" />
    </button>
    <button
      v-if="props.adminMode && props.canMoveRight"
      class="absolute right-0 top-1/2 -translate-y-1/2 translate-x-1/2 w-6 h-6 flex items-center justify-center rounded-full border border-brand-500 bg-brand-50 hover:bg-brand-100 shadow-sm z-10"
      title="Flytt til høyre"
      @click.stop="handleMoveRight"
    >
      <Icon name="ChevronRight" :size="12" class="text-brand-600" />
    </button>
  </div>

  <GroupMembersDialog
    v-if="props.groupId !== undefined && props.groupId !== null"
    ref="membersDialog"
    :group-id="props.groupId"
    :group-name="name"
    :admin-mode="props.adminMode"
    @member-count-changed="handleMemberCountChanged"
  />
  <StaffGroupsDialog
    v-if="props.staffGroups && props.staffGroups.length > 0"
    ref="staffDialog"
    :group-name="name"
    :staff-groups="props.staffGroups"
  />
</template>

<script lang="ts" setup>
import { ref } from "vue";
import GroupMembersDialog from "./GroupMembersDialog.vue";
import StaffGroupsDialog from "./StaffGroupsDialog.vue";
import Icon from "./ui/icon/Icon.vue";
import type { Group } from "@/types";

interface Props {
  name: string;
  title?: string;
  groupId?: number | string;
  memberCount: number;
  width?: number;
  height?: number;
  depth?: number;
  parentGroupId?: number | string | null;
  raw?: any;
  staffGroups?: Group[];
  adminMode?: boolean;
  isExpanded?: boolean;
  canMoveLeft?: boolean;
  canMoveRight?: boolean;
  onMemberCountChanged?: (groupId: number | string, count: number) => void;
  onMoveGroup?: (groupId: number | string, direction: "left" | "right") => void;
}

const props = withDefaults(defineProps<Props>(), {
  adminMode: false,
  isExpanded: false,
});

const membersDialog = ref<InstanceType<typeof GroupMembersDialog> | null>(null);
const staffDialog = ref<InstanceType<typeof StaffGroupsDialog> | null>(null);

const handleNodeClick = () => {
  // Priority: if has staff groups, show those first; otherwise show members dialog
  if (props.staffGroups && props.staffGroups.length > 0 && staffDialog.value) {
    staffDialog.value.open();
  } else if (membersDialog.value) {
    membersDialog.value.open();
  }
};

const handleMemberCountChanged = (count: number) => {
  if (
    props.groupId !== undefined &&
    props.groupId !== null &&
    props.onMemberCountChanged
  ) {
    props.onMemberCountChanged(props.groupId, count);
  }
};

const handleMoveLeft = () => {
  if (
    props.groupId !== undefined &&
    props.groupId !== null &&
    props.onMoveGroup
  ) {
    props.onMoveGroup(props.groupId, "left");
  }
};

const handleMoveRight = () => {
  if (
    props.groupId !== undefined &&
    props.groupId !== null &&
    props.onMoveGroup
  ) {
    props.onMoveGroup(props.groupId, "right");
  }
};
</script>
