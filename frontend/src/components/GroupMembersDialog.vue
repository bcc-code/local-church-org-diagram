<template>
    <BaseDialog ref="baseDialog" :title="`${groupName} - Medlemmer`" :description="dialogDescription">
        <div v-if="adminMode" class="mb-4 p-3.5 bg-gradient-to-br from-brand-50 to-neutral-50 border border-brand-200 rounded-lg">
            <div class="flex items-center gap-2 mb-2.5">
                <Icon name="UserPlus" :size="16" class="text-brand-600" />
                <div class="text-sm font-semibold text-neutral-900">Legg til medlem</div>
            </div>
            <PersonSearchInput @select="handleAddMember" />
        </div>

        <div v-if="hasError()" class="p-4 bg-red-50 border border-red-200 rounded-md">
            <p class="text-red-800 text-sm">{{ state.error }}</p>
        </div>
        <div v-else-if="!isLoading() && hasData()" class="space-y-2">
            <MemberCard v-for="member in state.data" :key="member.person_uid" :member="member" :admin-mode="adminMode"
                @remove="handleRemoveMember" />
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
import Icon from '@/components/ui/icon/Icon.vue';
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
const { fetchGroupMembers, addGroupMember, removeGroupMember } = useApiClient();
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

const handleAddMember = async (person: GroupMember) => {
    if (!props.groupId) return;

    try {
        await addGroupMember(props.groupId, person.person_uid);
        // Reload the members list
        await loadGroupMembers();
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
    } catch (error) {
        console.error('Failed to remove member:', error);
        // You could show an error message here
    }
};

defineExpose({
    open
});
</script>
