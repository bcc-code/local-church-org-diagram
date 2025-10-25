<template>
    <BaseDialog ref="baseDialog" :title="`${groupName} - Staber`" :description="dialogDescription">
        <div v-if="staffGroups.length > 0" class="space-y-3">
            <div v-for="staffGroup in staffGroups" :key="staffGroup.group_id"
                @click="() => handleStaffGroupClick(staffGroup)"
                class="p-3 border border-neutral-200 rounded-lg bg-neutral-0 hover:bg-neutral-50 transition-colors cursor-pointer">
                <div class="text-body-sm font-semibold text-neutral-900">
                    {{ staffGroup.label }}
                </div>
                <div class="text-caption text-neutral-600">
                    {{ staffGroup.member_count > 0 ? `${staffGroup.member_count} ${TEXTS.MEMBERS}` : `Ingen
                    ${TEXTS.MEMBERS}` }}
                </div>
            </div>
        </div>
    </BaseDialog>

    <GroupMembersDialog v-if="selectedStaffGroup" ref="membersDialog" :group-id="selectedStaffGroup.group_id"
        :group-name="selectedStaffGroup.label" />
</template>

<script lang="ts" setup>
import { ref, computed } from 'vue';
import BaseDialog from './BaseDialog.vue';
import GroupMembersDialog from './GroupMembersDialog.vue';
import { TEXTS } from '@/constants';
import type { Group } from '@/types';

interface Props {
    groupName: string;
    staffGroups: Group[];
}

const props = defineProps<Props>();

const baseDialog = ref<InstanceType<typeof BaseDialog> | null>(null);
const selectedStaffGroup = ref<Group | null>(null);
const membersDialog = ref<InstanceType<typeof GroupMembersDialog> | null>(null);

const dialogDescription = computed(() => {
    if (props.staffGroups.length === 0) return `Ingen ${TEXTS.STAFF_GROUPS} i denne gruppen.`;
    return `${props.staffGroups.length} ${TEXTS.STAFF_GROUPS} i gruppen:`;
});

const open = () => {
    if (baseDialog.value) {
        baseDialog.value.open();
    }
};

const handleStaffGroupClick = (staffGroup: Group) => {
    if (staffGroup.member_count > 0) {
        selectedStaffGroup.value = staffGroup;
        if (baseDialog.value) {
            baseDialog.value.close(); // Close staff groups dialog
        }
        setTimeout(() => {
            // Open members dialog after staff dialog closes
            if (membersDialog.value) {
                membersDialog.value.open();
            }
        }, 100);
    }
};

defineExpose({
    open
});
</script>