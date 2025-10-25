<template>
    <Dialog v-model:open="isOpen">
        <DialogContent class="max-w-md bg-neutral-0 border-neutral-200">
            <DialogHeader>
                <DialogTitle class="text-heading-lg text-neutral-900">{{ groupName }} - Medlemmer</DialogTitle>
                <DialogDescription class="text-body-sm text-neutral-600">
                    <span v-if="isLoading">Laster medlemmer...</span>
                    <span v-else-if="groupMembers.length === 0">Ingen medlemmer i denne gruppen.</span>
                    <span v-else>{{ groupMembers.length }} medlemmer i gruppen:</span>
                </DialogDescription>
            </DialogHeader>

            <div v-if="!isLoading" class="max-h-64 overflow-y-auto space-y-2 py-2">
                <MemberCard v-for="member in groupMembers" :key="member.person_uid" :member="member" />
            </div>

            <DialogFooter>
                <DialogClose as-child>
                    <Button variant="secondary"
                        class="bg-neutral-100 text-neutral-900 hover:bg-neutral-200">Lukk</Button>
                </DialogClose>
            </DialogFooter>
        </DialogContent>
    </Dialog>
</template>

<script lang="ts" setup>
import { ref } from 'vue';
import Button from '@/components/ui/button/Button.vue';
import Dialog from '@/components/ui/dialog/Dialog.vue';
import DialogContent from '@/components/ui/dialog/DialogContent.vue';
import DialogHeader from '@/components/ui/dialog/DialogHeader.vue';
import DialogTitle from '@/components/ui/dialog/DialogTitle.vue';
import DialogDescription from '@/components/ui/dialog/DialogDescription.vue';
import DialogFooter from '@/components/ui/dialog/DialogFooter.vue';
import DialogClose from '@/components/ui/dialog/DialogClose.vue';
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
const isOpen = ref(false);

const open = () => {
    isOpen.value = true;
    if (!groupMembers.value.length) {
        fetchGroupMembers();
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

// Expose the open method to parent component
defineExpose({
    open
});
</script>