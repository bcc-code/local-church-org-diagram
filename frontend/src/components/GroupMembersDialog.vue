<template>
    <Dialog>
        <DialogTrigger as-child>
            <Button variant="outline" size="icon">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.5v15m7.5-7.5h-15" />
                </svg>
            </Button>
        </DialogTrigger>
        <DialogContent class="max-w-md">
            <DialogHeader>
                <DialogTitle>{{ groupName }} - Medlemmer</DialogTitle>
                <DialogDescription>
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
                    <Button variant="secondary">Lukk</Button>
                </DialogClose>
            </DialogFooter>
        </DialogContent>
    </Dialog>
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import Button from '@/components/ui/button/Button.vue';
import Dialog from '@/components/ui/dialog/Dialog.vue';
import DialogTrigger from '@/components/ui/dialog/DialogTrigger.vue';
import DialogContent from '@/components/ui/dialog/DialogContent.vue';
import DialogHeader from '@/components/ui/dialog/DialogHeader.vue';
import DialogTitle from '@/components/ui/dialog/DialogTitle.vue';
import DialogDescription from '@/components/ui/dialog/DialogDescription.vue';
import DialogFooter from '@/components/ui/dialog/DialogFooter.vue';
import DialogClose from '@/components/ui/dialog/DialogClose.vue';
import MemberCard from './MemberCard.vue';

interface Props {
    groupId?: number | string;
    peopleIds?: string[];
    groupName: string;
}

interface GroupMember {
    name: string;
    person_uid: string;
    group_id: number;
}

const props = defineProps<Props>();

const groupMembers = ref<GroupMember[]>([]);
const isLoading = ref(true);

const fetchGroupMembers = async () => {
    if (!props.groupId) {
        isLoading.value = false;
        return;
    }

    try {
        const res = await fetch('/persons.json');
        const people: GroupMember[] = await res.json();

        // Filter people based on props.peopleIds if provided
        if (props.peopleIds && props.peopleIds.length > 0) {
            groupMembers.value = people.filter(person =>
                props.peopleIds!.includes(person.person_uid)
            );
        } else {
            // Fallback: filter by group_id if available
            groupMembers.value = people.filter(person =>
                person.group_id === Number(props.groupId)
            );
        }
    } catch (error) {
        console.error('Failed to fetch group members', error);
    } finally {
        isLoading.value = false;
    }
};

onMounted(() => {
    fetchGroupMembers();
});
</script>