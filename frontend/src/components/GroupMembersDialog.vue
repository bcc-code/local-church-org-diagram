<template>
    <BaseDialog ref="baseDialog" :title="`${groupName} - Medlemmer`" :description="dialogDescription">
        <div v-if="!isLoading" class="space-y-3">
            <MemberCard v-for="member in groupMembers" :key="member.person_uid" :member="member" />
        </div>
    </BaseDialog>
</template>

<script lang="ts" setup>
import { ref, computed } from 'vue';
import BaseDialog from './BaseDialog.vue';
import MemberCard from './MemberCard.vue';

interface Props {
    groupId?: number | string;
    memberCount?: number;
    groupName: string;
}

interface GroupMember {
    name: string;
    person_uid: string;
}

const props = defineProps<Props>();

const groupMembers = ref<GroupMember[]>([]);
const isLoading = ref(true);
const baseDialog = ref<InstanceType<typeof BaseDialog> | null>(null);

const dialogDescription = computed(() => {
    if (isLoading.value) return 'Laster medlemmer...';
    if (groupMembers.value.length === 0) return 'Ingen medlemmer i denne gruppen.';
    return `${groupMembers.value.length} medlemmer i gruppen:`;
});

const open = () => {
    if (baseDialog.value) {
        baseDialog.value.open();
        if (!groupMembers.value.length) {
            fetchGroupMembers();
        }
    }
};

const fetchGroupMembers = async () => {
    if (!props.groupId) {
        isLoading.value = false;
        return;
    }

    try {
        const res = await fetch('api/persons' + `?group_id=${props.groupId}`);
        const people: GroupMember[] = await res.json();
        groupMembers.value = [...people];

    } catch (error) {
        console.error('Failed to fetch group members', error);
    } finally {
        isLoading.value = false;
    }
};

defineExpose({
    open
});
</script>