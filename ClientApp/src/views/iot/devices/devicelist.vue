<template>
	<div class="device-list-page">
		<div class="device-list-page__crud">
			<fs-crud ref="crudRef" v-bind="crudBinding">
				<template #actionbar-right>
					<div class="device-list-page__actionbar">
						<span class="device-list-page__actionbar-tag">当前页 {{ deviceOverview.pageCount }}
							台</span>
						<span class="device-list-page__actionbar-tag is-primary">在线 {{
							deviceOverview.activeCount }} 台</span>
					</div>
				</template>
			</fs-crud>
		</div>

		<DeviceDetail ref="deviceDetailRef"></DeviceDetail>
		<addRules ref="addRulesRef"></addRules>
	</div>
</template>

<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';
import { useCrud, useExpose } from '@fast-crud/fast-crud';
import { useRoute } from 'vue-router';
import { storeToRefs } from 'pinia';
import DeviceDetail from './DeviceDetail.vue';
import addRules from './addRules.vue';
import { createDeviceCrudOptions } from '/@/views/iot/devices/deviceCrudOptions';
import { useUserInfo } from '/@/stores/userInfo';

const selectedItems = ref<any[]>([]);
const stores = useUserInfo();
const route = useRoute();
const { userInfos } = storeToRefs(stores);

const deviceOverview = reactive({
	total: 0,
	pageCount: 0,
	activeCount: 0,
	inactiveCount: 0,
	lastRefresh: '',
});

const deviceDetailRef = ref();
const crudRef = ref();
const crudBinding = ref();
const addRulesRef = ref();
const customerId = route.query.id || userInfos.value.customer.id;

const { crudExpose } = useExpose({ crudRef, crudBinding });
const { crudOptions } = createDeviceCrudOptions(
	{ expose: crudExpose },
	customerId,
	deviceDetailRef,
	addRulesRef,
	selectedItems,
	deviceOverview
);

// eslint-disable-next-line @typescript-eslint/no-unused-vars,no-unused-vars
const { resetCrudOptions } = useCrud({ expose: crudExpose, crudOptions });

const refreshDevices = () => {
	crudExpose.doRefresh();
};

onMounted(() => {
	refreshDevices();
});
</script>

<style lang="scss" scoped>
.device-list-page {
	display: flex;
	flex-direction: column;
	height: calc(100vh - 220px);
	min-height: 420px;
}

.device-list-page__crud {
	display: flex;
	flex-direction: column;
	flex: 1;
	min-height: 0;
}

.device-list-page__actionbar {
	display: flex;
	align-items: center;
	flex-wrap: wrap;
	justify-content: flex-end;
	gap: 16px;
}

.device-list-page__actionbar-tag {
	display: inline-flex;
	align-items: center;
	min-height: 32px;
	padding: 0 12px;
	border-radius: 999px;
	border: 1px solid rgba(226, 232, 240, 0.92);
	background: rgba(248, 250, 252, 0.9);
	color: #475569;
	font-size: 12px;
	font-weight: 600;
}

.device-list-page__actionbar-tag.is-primary {
	border-color: rgba(191, 219, 254, 0.92);
	background: rgba(219, 234, 254, 0.8);
	color: #2563eb;
}



:deep(.fs-crud .el-table) {
	border-radius: 20px;
	overflow: hidden;
}

:deep(.fs-crud .el-table th.el-table__cell) {
	background: #f8fbff;
}

:deep(.fs-crud .el-pagination) {
	margin-top: 18px;
}

@media (max-width: 767px) {
	.device-list-page {
		height: auto;
		min-height: 0;
	}

	.device-list-page__crud {
		min-height: auto;
	}
}
</style>
