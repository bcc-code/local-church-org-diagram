<template>
    <BaseDialog ref="baseDialog" :title="`${groupName}`" :description="dialogDescription">
        <div v-if="adminMode" class="mb-3">
            <PersonSearchInput @select="handleAddMember" />
        </div>

        <div v-if="hasError()" class="p-4 bg-red-50 border border-red-200 rounded-md">
            <p class="text-red-800 text-sm">{{ state.error }}</p>
        </div>
        <div v-else-if="!isLoading() && hasData()" class="space-y-2 overflow-y-auto max-h-[60vh]">
            <MemberCard v-for="member in state.data" :key="member.person_uid" :member="member" :admin-mode="adminMode"
                @remove="handleRemoveMember" @update-title="handleUpdateTitle" />
        </div>
        <div v-else-if="!isLoading() && !hasData()" class="p-4 text-center text-neutral-600">
            <p>{{ TEXTS.NO_MEMBERS }}</p>
        </div>
    </BaseDialog>
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue';
import BaseDialog from './BaseDialog.vue';
import MemberCard from './MemberCard.vue';
import PersonSearchInput from './PersonSearchInput.vue';
import { useAsyncData, useApiClient } from '@/composables/useApi';
import { TEXTS } from '@/constants';
import type { GroupMember } from '@/types';

interface Props {
    groupId?: number | string;
    memberCount?: number;
    groupName: string;
    adminMode?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
    adminMode: false,
});

const { state, execute, isLoading, hasError, hasData } = useAsyncData<GroupMember[]>();
const { fetchGroupMembers, addGroupMember, removeGroupMember, updateMemberTitle } = useApiClient();
const baseDialog = ref<InstanceType<typeof BaseDialog> | null>(null);

const dialogDescription = computed(() => {
    if (isLoading()) return TEXTS.LOADING_MEMBERS;
    if (hasError()) return TEXTS.COULD_NOT_LOAD_MEMBERS;
    if (hasData() && state.value.data) return `${state.value.data.length} ${TEXTS.MEMBERS} i gruppen:`;
    return TEXTS.NO_MEMBERS;
});

const open = () => {
    if (baseDialog.value) {
        baseDialog.value.open();
        if (!hasData() && props.groupId) {
            loadGroupMembers();
        }
    }
};

const loadGroupMembers = async () => {
    if (!props.groupId) return;

    await execute(() => fetchGroupMembers(props.groupId!));
};

const emit = defineEmits<{
    'member-count-changed': [count: number]
}>();

const handleAddMember = async (person: GroupMember) => {
    if (!props.groupId) return;

    try {
        await addGroupMember(props.groupId, person.person_uid);
        // Reload the members list
        await loadGroupMembers();
        // Emit the new count
        if (state.value.data) {
            emit('member-count-changed', state.value.data.length);
        }
    } catch (error) {
        console.error('Failed to add member:', error);
        // You could show an error message here
    }
};

const handleRemoveMember = async (personUid: string) => {
    if (!props.groupId) return;

    try {
        await removeGroupMember(props.groupId, personUid);
        // Reload the members list
        await loadGroupMembers();
        // Emit the new count
        if (state.value.data) {
            emit('member-count-changed', state.value.data.length);
        }
    } catch (error) {
        console.error('Failed to remove member:', error);
        // You could show an error message here
    }
};

const handleUpdateTitle = async (personUid: string, title: string) => {
    if (!props.groupId) return;

    try {
        await updateMemberTitle(props.groupId, personUid, title);
        // Reload the members list to get the updated data
        await loadGroupMembers();
    } catch (error) {
        console.error('Failed to update member title:', error);
        // You could show an error message here
    }
};

defineExpose({
    open
});
</script>
