import { createRouter, createWebHistory } from "vue-router";
import RegularView from "./views/RegularView.vue";
import { useAuth } from "./composables/useAuth";

const routes = [
  {
    path: "/",
    name: "home",
    component: RegularView,
  },
  // {
  //   path: "/:tenantId/admin",
  //   name: "admin",
  //   component: AdminView,
  //   props: true,
  // },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach(async () => {
  const { loaded, fetchCurrentUser } = useAuth();
  if (!loaded.value) {
    try {
      await fetchCurrentUser();
    } catch {
      return false; // fetchWithAuth already redirects to /login on 401
    }
  }

  return true;
});

export default router;

