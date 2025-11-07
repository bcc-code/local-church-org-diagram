import { createRouter, createWebHistory } from "vue-router";
import RegularView from "./views/RegularView.vue";
import AdminView from "./views/AdminView.vue";

const routes = [
  {
    path: "/",
    name: "home",
    component: RegularView,
  },
  {
    path: "/:tenantId/admin",
    name: "admin",
    component: AdminView,
    props: true,
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

export default router;

