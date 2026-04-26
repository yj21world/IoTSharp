<template>
	<div class="auth-page">
		<section class="auth-panel">
			<div class="auth-panel__brand">
				<AppLogo />
			</div>
			<div class="auth-panel__header">
				<div class="auth-panel__eyebrow">Console Login</div>
				<h1>{{ pageTitle }}</h1>
				<p>请输入账号和密码登录控制台。</p>
			</div>

			<Account />

			<div class="auth-panel__footer">
				<div>{{ currentYear }} {{ pageTitle }}</div>
			</div>
		</section>
	</div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { storeToRefs } from 'pinia';
import { useThemeConfig } from '/@/stores/themeConfig';
import { NextLoading } from '/@/utils/loading';
import AppLogo from '/@/components/AppLogo.vue';
import Account from '/@/views/login/component/account.vue';

const storesThemeConfig = useThemeConfig();
const { themeConfig } = storeToRefs(storesThemeConfig);

const pageTitle = computed(() => themeConfig.value.globalTitle || 'IoTSharp');
const currentYear = new Date().getFullYear();

onMounted(() => {
	NextLoading.done();
});
</script>

<style scoped lang="scss">
.auth-page {
	display: flex;
	align-items: center;
	justify-content: center;
	min-height: 100vh;
	padding: 24px;
	background:
		linear-gradient(rgba(148, 163, 184, 0.08) 1px, transparent 1px),
		linear-gradient(90deg, rgba(148, 163, 184, 0.08) 1px, transparent 1px),
		linear-gradient(180deg, #f8fbff 0%, #eef6ff 100%);
	background-size: 48px 48px, 48px 48px, auto;
	overflow-x: hidden;
	overflow-y: auto;
	overscroll-behavior-y: contain;
	scrollbar-gutter: stable;
	-webkit-overflow-scrolling: touch;
}

.auth-panel {
	display: flex;
	flex-direction: column;
	gap: 22px;
	width: min(440px, 100%);
	padding: 36px;
	border: 1px solid rgba(226, 232, 240, 0.92);
	border-radius: 24px;
	background: rgba(255, 255, 255, 0.96);
	box-shadow: 0 24px 60px rgba(15, 23, 42, 0.12);
}

.auth-panel__brand {
	display: flex;
	justify-content: center;

	:deep(.app-logo) {
		--app-logo-text: #123b6d;
		--app-logo-subtext: #64748b;
	}
}

.auth-panel__eyebrow {
	margin-bottom: 10px;
	color: #2563eb;
	font-size: 12px;
	font-weight: 700;
	letter-spacing: 0.12em;
	text-transform: uppercase;
	text-align: center;
}

.auth-panel__header h1 {
	margin: 0 0 10px;
	color: #123b6d;
	font-size: 30px;
	line-height: 1.2;
	text-align: center;
}

.auth-panel__header p {
	margin: 0;
	color: #64748b;
	font-size: 14px;
	line-height: 1.7;
	text-align: center;
}

.auth-panel__footer {
	padding-top: 18px;
	border-top: 1px solid rgba(226, 232, 240, 0.9);
	color: #64748b;
	font-size: 12px;
	text-align: center;
}

@media (max-width: 767px) {
	.auth-page {
		align-items: stretch;
		padding: 16px;
	}

	.auth-panel {
		justify-content: center;
		min-height: calc(100vh - 32px);
		padding: 24px;
	}
}
</style>
