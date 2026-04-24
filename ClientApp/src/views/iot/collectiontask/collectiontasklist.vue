<template>
	<div class="collection-task-page">
		<ConsoleCrudWorkspace
			eyebrow="Collection Workspace"
			title="采集任务管理"
			description="集中管理网关采集任务、从站点位和采集日志，优先打通 HVAC 云端采集配置闭环。"
			card-eyebrow="Task Table"
			card-title="采集任务列表"
			card-description="支持创建、编辑、启停任务，并查看当前配置下的从站数量、点位数量和最近日志。"
			:badges="badges"
			:metrics="metrics"
		>
			<template #actions>
				<el-button type="primary" @click="loadTasks">刷新列表</el-button>
				<el-button type="success" @click="openCreateDialog">新增任务</el-button>
			</template>

			<template #aside>
				<div class="collection-task-page__scope">
					<span>采集任务工作区</span>
					<strong>{{ state.tableData.total }}</strong>
					<small>当前展示 HVAC 采集任务与日志入口</small>
				</div>
			</template>

			<div class="collection-task-page__filters">
				<el-input v-model="query.taskKey" placeholder="按任务标识筛选" clearable @keyup.enter="handleSearch" />
				<el-select v-model="query.protocol" placeholder="协议" clearable @change="handleSearch">
					<el-option label="全部协议" value="" />
					<el-option label="Modbus" value="Modbus" />
					<el-option label="OpcUa" value="OpcUa" />
				</el-select>
				<el-button type="primary" @click="handleSearch">查询</el-button>
				<el-button @click="resetFilters">重置</el-button>
			</div>

			<el-table :data="filteredRows" row-key="id" v-loading="state.tableData.loading" class="collection-task-page__table">
				<el-table-column type="expand">
					<template #default="props">
						<div class="task-expand">
							<div class="task-expand__summary">
								<el-tag type="primary">网关 {{ props.row.edgeNodeId || '--' }}</el-tag>
								<el-tag>{{ props.row.devices?.length || 0 }} 个从站</el-tag>
								<el-tag type="success">{{ getPointCount(props.row) }} 个点位</el-tag>
							</div>
							<el-table :data="props.row.devices || []" size="small">
								<el-table-column prop="deviceKey" label="从站标识" width="180" />
								<el-table-column prop="deviceName" label="从站名称" min-width="160" />
								<el-table-column label="启用" width="90">
									<template #default="scope">
										<el-tag :type="scope.row.enabled ? 'success' : 'info'" size="small">{{ scope.row.enabled ? '启用' : '禁用' }}</el-tag>
									</template>
								</el-table-column>
								<el-table-column label="点位数" width="100">
									<template #default="scope">
										{{ scope.row.points?.length || 0 }}
									</template>
								</el-table-column>
								<el-table-column label="点位预览" min-width="320">
									<template #default="scope">
										<div class="task-expand__points">
											<span v-for="point in (scope.row.points || []).slice(0, 4)" :key="point.pointKey" class="task-expand__point-chip">
												{{ point.pointKey }}
											</span>
											<span v-if="(scope.row.points || []).length > 4" class="task-expand__point-chip is-muted">
												+{{ (scope.row.points || []).length - 4 }}
											</span>
										</div>
									</template>
								</el-table-column>
							</el-table>
						</div>
					</template>
				</el-table-column>

				<el-table-column prop="taskKey" label="任务标识" min-width="220">
					<template #default="scope">
						<div class="task-key-cell">
							<strong>{{ scope.row.taskKey }}</strong>
							<small>{{ scope.row.protocol }}</small>
						</div>
					</template>
				</el-table-column>

				<el-table-column prop="protocol" label="协议" width="120" />
				<el-table-column label="从站数" width="100">
					<template #default="scope">{{ scope.row.devices?.length || 0 }}</template>
				</el-table-column>
				<el-table-column label="点位数" width="100">
					<template #default="scope">{{ getPointCount(scope.row) }}</template>
				</el-table-column>
				<el-table-column label="状态" width="100">
					<template #default="scope">
						<el-tag :type="scope.row.enabled ? 'success' : 'info'">{{ scope.row.enabled ? '启用' : '停用' }}</el-tag>
					</template>
				</el-table-column>
				<el-table-column prop="version" label="版本" width="80" />
				<el-table-column label="操作" width="320">
					<template #default="scope">
						<el-button size="small" text type="primary" @click="openEditDialog(scope.row)">编辑</el-button>
						<el-button size="small" text :type="scope.row.enabled ? 'warning' : 'success'" @click="toggleTask(scope.row)">
							{{ scope.row.enabled ? '停用' : '启用' }}
						</el-button>
						<el-button size="small" text type="primary" @click="openLogDialog(scope.row)">日志</el-button>
						<el-button size="small" text type="danger" @click="deleteTask(scope.row.id)">删除</el-button>
					</template>
				</el-table-column>
			</el-table>
		</ConsoleCrudWorkspace>

		<el-dialog v-model="dialogVisible" :title="dialogTitle" width="960px" destroy-on-close>
			<el-form :model="form" label-width="120px">
				<el-row :gutter="16">
					<el-col :span="12">
						<el-form-item label="任务标识">
							<el-input v-model="form.taskKey" placeholder="如 hvac-boiler-room-a" />
						</el-form-item>
					</el-col>
					<el-col :span="12">
						<el-form-item label="协议">
							<el-select v-model="form.protocol" style="width: 100%">
								<el-option label="Modbus" value="Modbus" />
								<el-option label="OpcUa" value="OpcUa" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
				<el-row :gutter="16">
					<el-col :span="12">
						<el-form-item label="网关设备ID">
							<el-input v-model="form.edgeNodeId" placeholder="输入网关 DeviceId" />
						</el-form-item>
					</el-col>
					<el-col :span="12">
						<el-form-item label="连接名称">
							<el-input v-model="form.connection.connectionName" />
						</el-form-item>
					</el-col>
				</el-row>
				<el-row :gutter="16">
					<el-col :span="8">
						<el-form-item label="Transport">
							<el-input v-model="form.connection.transport" />
						</el-form-item>
					</el-col>
					<el-col :span="8">
						<el-form-item label="主机">
							<el-input v-model="form.connection.host" />
						</el-form-item>
					</el-col>
					<el-col :span="8">
						<el-form-item label="端口">
							<el-input-number v-model="form.connection.port" :min="0" style="width: 100%" />
						</el-form-item>
					</el-col>
				</el-row>
				<div class="collection-task-page__editor-head">
					<span>从站与点位 JSON</span>
					<el-button size="small" @click="appendSampleDevice">追加示例从站</el-button>
				</div>
				<el-input
					v-model="devicesJson"
					type="textarea"
					:rows="16"
					placeholder="请输入 devices JSON 数组"
				/>
			</el-form>
			<template #footer>
				<el-button @click="dialogVisible = false">取消</el-button>
				<el-button type="primary" @click="submitForm">保存</el-button>
			</template>
		</el-dialog>

		<el-dialog v-model="logDialogVisible" title="采集日志" width="960px" destroy-on-close>
			<el-table :data="logs.rows" v-loading="logs.loading">
				<el-table-column prop="requestAt" label="请求时间" min-width="180" />
				<el-table-column prop="status" label="状态" width="120" />
				<el-table-column prop="requestFrame" label="请求帧" min-width="220" show-overflow-tooltip />
				<el-table-column prop="responseFrame" label="响应帧" min-width="220" show-overflow-tooltip />
				<el-table-column prop="errorMessage" label="错误" min-width="180" show-overflow-tooltip />
			</el-table>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import ConsoleCrudWorkspace from '/@/components/console/ConsoleCrudWorkspace.vue';
import { collectionTaskApi } from '/@/api/collectiontask';

const api = collectionTaskApi();

const state = reactive({
	tableData: {
		rows: [] as any[],
		total: 0,
		loading: false,
	},
});

const query = reactive({
	taskKey: '',
	protocol: '',
});

const dialogVisible = ref(false);
const dialogTitle = ref('新建采集任务');
const editingId = ref('');
const devicesJson = ref('[]');
const form = reactive<any>(createEmptyForm());

const logDialogVisible = ref(false);
const currentLogTask = ref<any>(null);
const logs = reactive({
	rows: [] as any[],
	loading: false,
});

function createEmptyForm() {
	return {
		id: '',
		taskKey: '',
		protocol: 'Modbus',
		version: 1,
		edgeNodeId: '',
		connection: {
			connectionKey: 'default-connection',
			connectionName: '默认连接',
			protocol: 'Modbus',
			transport: 'MqttTransparent',
			host: '',
			port: 1883,
			timeoutMs: 3000,
			retryCount: 3,
		},
		devices: [],
		reportPolicy: {
			defaultTrigger: 'OnChange',
			includeQuality: true,
			includeTimestamp: true,
		},
	};
}

const filteredRows = computed(() => {
	return state.tableData.rows.filter((item) => {
		const matchesTask = !query.taskKey || item.taskKey?.toLowerCase().includes(query.taskKey.toLowerCase());
		const matchesProtocol = !query.protocol || item.protocol === query.protocol;
		return matchesTask && matchesProtocol;
	});
});

const badges = computed(() => [
	query.taskKey ? `任务 ${query.taskKey}` : '全部任务',
	query.protocol ? `协议 ${query.protocol}` : '全部协议',
	`展示 ${filteredRows.value.length} / ${state.tableData.total}`,
]);

const metrics = computed(() => [
	{ label: '任务总数', value: state.tableData.total, hint: '当前工作区内已创建的采集任务。', tone: 'primary' as const },
	{ label: '启用任务', value: state.tableData.rows.filter((item) => item.enabled).length, hint: '已启用并会被调度器加载的任务。', tone: 'success' as const },
	{ label: '从站总数', value: state.tableData.rows.reduce((sum, item) => sum + (item.devices?.length || 0), 0), hint: '当前列表所有任务下的从站数量。', tone: 'accent' as const },
	{ label: '点位总数', value: state.tableData.rows.reduce((sum, item) => sum + getPointCount(item), 0), hint: '当前列表所有任务下的采集点位数量。', tone: 'warning' as const },
]);

function getPointCount(task: any) {
	return (task.devices || []).reduce((sum: number, device: any) => sum + (device.points?.length || 0), 0);
}

async function loadTasks() {
	state.tableData.loading = true;
	try {
		const res = await api.getAll();
		if (res.code === 10000) {
			state.tableData.rows = res.data?.rows || [];
			state.tableData.total = Number(res.data?.total ?? state.tableData.rows.length);
		} else {
			ElMessage.error(res.msg || '获取采集任务失败');
		}
	} finally {
		state.tableData.loading = false;
	}
}

function handleSearch() {}

function resetFilters() {
	query.taskKey = '';
	query.protocol = '';
}

function openCreateDialog() {
	editingId.value = '';
	dialogTitle.value = '新建采集任务';
	Object.assign(form, createEmptyForm());
	devicesJson.value = '[]';
	dialogVisible.value = true;
}

function openEditDialog(row: any) {
	editingId.value = row.id;
	dialogTitle.value = '编辑采集任务';
	Object.assign(form, JSON.parse(JSON.stringify(row)));
	devicesJson.value = JSON.stringify(row.devices || [], null, 2);
	dialogVisible.value = true;
}

function appendSampleDevice() {
	const sample = [
		...(safeParseDevicesJson() || []),
		{
			deviceKey: `slave-${Date.now()}`,
			deviceName: '示例从站',
			enabled: true,
			protocolOptions: {
				SlaveId: 1,
			},
			points: [
				{
					pointKey: 'supply-temp',
					pointName: '供水温度',
					sourceType: 'HoldingRegister',
					address: '0',
					rawValueType: 'float32',
					length: 2,
					polling: { readPeriodMs: 10000, group: 'fast' },
					transforms: [],
					mapping: {
						targetType: 'Telemetry',
						targetName: 'supplyTemperature',
						valueType: 'Double',
						displayName: '供水温度',
						unit: '°C',
						group: 'default',
					},
				},
			],
		},
	];
	devicesJson.value = JSON.stringify(sample, null, 2);
}

function safeParseDevicesJson() {
	try {
		return JSON.parse(devicesJson.value || '[]');
	} catch {
		return null;
	}
}

async function submitForm() {
	const devices = safeParseDevicesJson();
	if (!devices) {
		ElMessage.error('从站与点位 JSON 不是合法格式');
		return;
	}

	const payload = {
		...form,
		connection: {
			...form.connection,
			protocol: form.protocol,
		},
		devices,
	};

	const validateResult = await api.validateDraft(payload);
	if (validateResult.code !== 10000) {
		ElMessage.error(validateResult.msg || '任务校验失败');
		return;
	}

	const res = editingId.value ? await api.update(editingId.value, payload) : await api.create(payload);
	if (res.code === 10000) {
		ElMessage.success(editingId.value ? '更新成功' : '创建成功');
		dialogVisible.value = false;
		await loadTasks();
	} else {
		ElMessage.error(res.msg || '保存失败');
	}
}

async function toggleTask(row: any) {
	const res = row.enabled ? await api.disable(row.id) : await api.enable(row.id);
	if (res.code === 10000) {
		ElMessage.success(row.enabled ? '任务已停用' : '任务已启用');
		await loadTasks();
	} else {
		ElMessage.error(res.msg || '状态切换失败');
	}
}

async function deleteTask(id: string) {
	try {
		await ElMessageBox.confirm('确定删除该采集任务？', '提示', { type: 'warning' });
		const res = await api.delete(id);
		if (res.code === 10000) {
			ElMessage.success('删除成功');
			await loadTasks();
		} else {
			ElMessage.error(res.msg || '删除失败');
		}
	} catch {}
}

async function openLogDialog(row: any) {
	currentLogTask.value = row;
	logDialogVisible.value = true;
	logs.loading = true;
	try {
		const res = await api.getLogs({ gatewayDeviceId: row.edgeNodeId, limit: 50, offset: 0 });
		if (res.code === 10000) {
			logs.rows = res.data?.rows || [];
		} else {
			ElMessage.error(res.msg || '获取日志失败');
		}
	} finally {
		logs.loading = false;
	}
}

onMounted(() => {
	loadTasks();
});
</script>

<style scoped lang="scss">
.collection-task-page {
	display: flex;
	flex-direction: column;
	gap: 18px;
}

.collection-task-page__scope {
	display: flex;
	flex-direction: column;
	align-items: flex-end;
	min-width: 180px;
	padding: 14px 16px;
	border-radius: 20px;
	border: 1px solid rgba(191, 219, 254, 0.9);
	background: rgba(255, 255, 255, 0.78);
}

.collection-task-page__scope span {
	color: #64748b;
	font-size: 12px;
}

.collection-task-page__scope strong {
	margin-top: 8px;
	color: #123b6d;
	font-size: 30px;
	line-height: 1;
}

.collection-task-page__scope small {
	margin-top: 8px;
	color: #7c8da1;
	font-size: 12px;
	line-height: 1.6;
	text-align: right;
}

.collection-task-page__filters {
	display: flex;
	flex-wrap: wrap;
	gap: 12px;
	margin-bottom: 18px;
}

.collection-task-page__filters .el-input,
.collection-task-page__filters .el-select {
	width: 240px;
}

.task-key-cell {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.task-key-cell strong {
	color: #123b6d;
}

.task-key-cell small {
	color: #64748b;
}

.task-expand {
	padding: 16px;
	background: #f8fbff;
	border-radius: 8px;
}

.task-expand__summary {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	margin-bottom: 12px;
}

.task-expand__points {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
}

.task-expand__point-chip {
	padding: 4px 10px;
	border-radius: 999px;
	background: rgba(18, 59, 109, 0.08);
	color: #123b6d;
	font-size: 12px;
}

.task-expand__point-chip.is-muted {
	background: rgba(100, 116, 139, 0.12);
	color: #64748b;
}

.collection-task-page__editor-head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	margin-bottom: 10px;
	color: #123b6d;
	font-weight: 600;
}

:deep(.collection-task-page__table) {
	border-radius: 20px;
	overflow: hidden;
}

:deep(.collection-task-page__table th.el-table__cell) {
	background: #f8fbff;
}
</style>
