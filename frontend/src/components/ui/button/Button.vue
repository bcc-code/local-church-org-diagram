<script lang="ts" setup>
import { Primitive } from 'radix-vue';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const buttonVariants = cva(
    'inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50',
    {
        variants: {
            variant: {
                default: 'bg-primary text-primary-foreground shadow hover:opacity-90',
                secondary: 'bg-secondary text-secondary-foreground hover:opacity-90',
                outline: 'border border-input bg-background hover:bg-accent hover:text-accent-foreground',
                ghost: 'hover:bg-accent hover:text-accent-foreground',
                link: 'text-primary underline-offset-4 hover:underline',
                destructive: 'bg-destructive text-destructive-foreground hover:opacity-90',
            },
            size: {
                default: 'h-9 px-4 py-2',
                sm: 'h-8 rounded-md px-3',
                lg: 'h-10 rounded-md px-8',
                icon: 'h-9 w-9',
            },
        },
        defaultVariants: {
            variant: 'default',
            size: 'default',
        },
    }
);

type ButtonVariants = VariantProps<typeof buttonVariants>;
const props = withDefaults(
    defineProps<{
        as?: string;
        class?: string;
        variant?: ButtonVariants['variant'];
        size?: ButtonVariants['size'];
    }>(),
    {}
);
</script>

<template>
    <Primitive :as="props.as ?? 'button'"
        :class="cn(buttonVariants({ variant: props.variant, size: props.size }), props.class)">
        <slot />
    </Primitive>

</template>
