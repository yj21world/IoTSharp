<template>
	<div class="collection-task-page">
		<div class="collection-task-page__content">
			<div class="collection-task-page__filters">
				<el-input v-model="query.taskKey" placeholder="任务标识" clearable @keyup.enter="handleSearch" />
				<el-input v-model="query.gateway" placeholder="网关设备ID" clearable @keyup.enter="handleSearch" />
				<el-select v-model="query.protocol" placeholder="协议" clearable @change="handleSearch">
					<el-option label="全部协议" value="" />
					<el-option label="Modbus" value="Modbus" />
					<el-option label="OpcUa" value="OpcUa" />
				</el-select>
				<el-select v-model="query.enabled" placeholder="状态" clearable @change="handleSearch">
					<el-option label="全部状态" value="" />
					<el-option label="启用" value="true" />
					<el-option label="停用" value="false" />
				</el-select>
				<el-button type="primary" @click="handleSearch">查询</el-button>
				<el-button @click="resetFilters">重置</el-button>
				<el-button type="success" @click="openCreateDialog">新增任务</el-button>
			</div>

			<el-table
				:data="pagedRows"
				row-key="id"
				v-loading="state.tableData.loading"
				class="collection-task-page__table"
				empty-text="暂无采集任务"
			>
				<el-table-column type="expand">
					<template #default="props">
						<div class="task-expand">
							<div class="task-expand__summary">
								<el-tag type="primary">网关 {{ props.row.edgeNodeId || '--' }}</el-tag>
								<el-tag>{{ props.row.devices?.length || 0 }} 个从站</el-tag>
								<el-tag type="success">{{ getPointCount(props.row) }} 个点位</el-tag>
							</div>
							<el-table :data="props.row.devices || []" size="small" empty-text="暂无从站">
								<el-table-column prop="deviceKey" label="从站标识" width="180" />
								<el-table-column prop="deviceName" label="从站名称" min-width="160" />
								<el-table-column label="SlaveId" width="100">
									<template #default="scope">{{ getSlaveId(scope.row) }}</template>
								</el-table-column>
								<el-table-column label="启用" width="90">
									<template #default="scope">
										<el-tag :type="scope.row.enabled ? 'success' : 'info'" size="small">
											{{ scope.row.enabled ? '启用' : '停用' }}
										</el-tag>
									</template>
								</el-table-column>
								<el-table-column label="点位数" width="100">
									<template #default="scope">{{ scope.row.points?.length || 0 }}</template>
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
							<small>{{ scope.row.connection?.connectionName || scope.row.protocol }}</small>
						</div>
					</template>
				</el-table-column>
				<el-table-column prop="edgeNodeId" label="网关" min-width="220" show-overflow-tooltip />
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
				<el-table-column label="操作" width="320" fixed="right">
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

			<div class="collection-task-page__pagination">
				<el-pagination
					v-model:current-page="pagination.page"
					v-model:page-size="pagination.pageSize"
					:page-sizes="[10, 20, 50, 100]"
					layout="total, sizes, prev, pager, next, jumper"
					:total="filteredRows.length"
				/>
			</div>
		</div>

		<el-dialog v-model="dialogVisible" :title="dialogTitle" width="1180px" destroy-on-close class="collection-task-editor">
			<el-form ref="formRef" :model="form" :rules="formRules" label-width="120px">
				<el-tabs v-model="activeEditorTab">
					<el-tab-pane label="基础信息" name="base">
						<el-row :gutter="16">
							<el-col :span="12">
								<el-form-item label="任务标识" prop="taskKey">
									<el-input v-model="form.taskKey" placeholder="如 mall-a-cooling-pump" />
								</el-form-item>
							</el-col>
							<el-col :span="12">
								<el-form-item label="网关设备ID" prop="edgeNodeId">
									<el-input v-model="form.edgeNodeId" placeholder="输入网关 DeviceId" />
								</el-form-item>
							</el-col>
						</el-row>
						<el-row :gutter="16">
							<el-col :span="8">
								<el-form-item label="协议" prop="protocol">
									<el-select v-model="form.protocol" style="width: 100%">
										<el-option label="Modbus" value="Modbus" />
										<el-option label="OpcUa" value="OpcUa" />
									</el-select>
								</el-form-item>
							</el-col>
							<el-col :span="8">
								<el-form-item label="启用">
									<el-switch v-model="form.enabled" />
								</el-form-item>
							</el-col>
							<el-col :span="8">
								<el-form-item label="版本">
									<el-input-number v-model="form.version" :min="1" style="width: 100%" />
								</el-form-item>
							</el-col>
						</el-row>
						<el-row :gutter="16">
							<el-col :span="12">
								<el-form-item label="连接名称" prop="connection.connectionName">
									<el-input v-model="form.connection.connectionName" />
								</el-form-item>
							</el-col>
							<el-col :span="12">
								<el-form-item label="Transport">
									<el-input v-model="form.connection.transport" />
								</el-form-item>
							</el-col>
						</el-row>
						<el-row :gutter="16">
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
							<el-col :span="8">
								<el-form-item label="超时(ms)">
									<el-input-number v-model="form.connection.timeoutMs" :min="100" :step="100" style="width: 100%" />
								</el-form-item>
							</el-col>
						</el-row>
					</el-tab-pane>

					<el-tab-pane label="从站配置" name="devices">
						<div class="collection-task-page__table-toolbar">
							<el-button type="primary" @click="addDevice">新增从站</el-button>
						</div>
						<el-table :data="form.devices" row-key="_rowId" class="editor-table" empty-text="暂无从站">
							<el-table-column label="从站标识" min-width="180">
								<template #default="scope">
									<el-input v-model="scope.row.deviceKey" placeholder="pump-vfd-01" />
								</template>
							</el-table-column>
							<el-table-column label="从站名称" min-width="180">
								<template #default="scope">
									<el-input v-model="scope.row.deviceName" placeholder="冷冻水泵 1# 变频器" />
								</template>
							</el-table-column>
							<el-table-column label="SlaveId" width="140">
								<template #default="scope">
									<el-input-number v-model="scope.row.slaveId" :min="1" :max="247" style="width: 100%" />
								</template>
							</el-table-column>
							<el-table-column label="启用" width="90">
								<template #default="scope">
									<el-switch v-model="scope.row.enabled" />
								</template>
							</el-table-column>
							<el-table-column label="点位数" width="100">
								<template #default="scope">{{ scope.row.points.length }}</template>
							</el-table-column>
							<el-table-column label="操作" width="260" fixed="right">
								<template #default="scope">
									<el-button size="small" text type="primary" @click="selectDevice(scope.$index)">编辑点位</el-button>
									<el-button size="small" text @click="copyDevice(scope.$index)">复制</el-button>
									<el-button size="small" text type="danger" @click="removeDevice(scope.$index)">删除</el-button>
								</template>
							</el-table-column>
						</el-table>
					</el-tab-pane>

					<el-tab-pane label="点位配置" name="points">
						<div class="collection-task-page__table-toolbar">
							<el-select v-model="selectedDeviceIndex" placeholder="选择从站" style="width: 260px">
								<el-option
									v-for="(device, index) in form.devices"
									:key="device._rowId"
									:label="device.deviceName || device.deviceKey || `从站 ${index + 1}`"
									:value="index"
								/>
							</el-select>
							<el-button type="primary" :disabled="selectedDeviceIndex < 0" @click="addPoint">新增点位</el-button>
							<el-button :disabled="selectedDeviceIndex < 0 || selectedPoints.length === 0" @click="batchSetPointEnabled(true)">批量启用</el-button>
							<el-button :disabled="selectedDeviceIndex < 0 || selectedPoints.length === 0" @click="batchSetPointEnabled(false)">批量停用</el-button>
						</div>
						<el-table
							:data="selectedPoints"
							row-key="_rowId"
							class="editor-table point-editor-table"
							empty-text="暂无点位"
						>
							<el-table-column label="点位标识" min-width="160">
								<template #default="scope">
									<el-input v-model="scope.row.pointKey" placeholder="frequency" />
								</template>
							</el-table-column>
							<el-table-column label="点位名称" min-width="160">
								<template #default="scope">
									<el-input v-model="scope.row.pointName" placeholder="运行频率" />
								</template>
							</el-table-column>
							<el-table-column label="功能码" width="130">
								<template #default="scope">
									<el-select v-model="scope.row.sourceType">
										<el-option label="01 Coil" value="Coil" />
										<el-option label="02 Discrete" value="DiscreteInput" />
										<el-option label="03 Holding" value="HoldingRegister" />
										<el-option label="04 Input" value="InputRegister" />
									</el-select>
								</template>
							</el-table-column>
							<el-table-column label="地址" width="130">
								<template #default="scope">
									<el-input-number v-model="scope.row.address" :min="0" style="width: 100%" />
								</template>
							</el-table-column>
							<el-table-column label="数量" width="120">
								<template #default="scope">
									<el-input-number v-model="scope.row.length" :min="1" style="width: 100%" />
								</template>
							</el-table-column>
							<el-table-column label="原始类型" width="140">
								<template #default="scope">
									<el-select v-model="scope.row.rawValueType">
										<el-option v-for="item in rawValueTypes" :key="item" :label="item" :value="item" />
									</el-select>
								</template>
							</el-table-column>
							<el-table-column label="字节序" width="120">
								<template #default="scope">
									<el-select v-model="scope.row.byteOrder">
										<el-option v-for="item in byteOrders" :key="item" :label="item" :value="item" />
									</el-select>
								</template>
							</el-table-column>
							<el-table-column label="字顺序" width="120">
								<template #default="scope">
									<el-select v-model="scope.row.wordOrder">
										<el-option label="AB" value="AB" />
										<el-option label="BA" value="BA" />
									</el-select>
								</template>
							</el-table-column>
							<el-table-column label="周期(ms)" width="150">
								<template #default="scope">
									<el-input-number v-model="scope.row.readPeriodMs" :min="1000" :step="1000" style="width: 100%" />
								</template>
							</el-table-column>
							<el-table-column label="目标遥测键" min-width="160">
								<template #default="scope">
									<el-input v-model="scope.row.targetName" placeholder="frequency" />
								</template>
							</el-table-column>
							<el-table-column label="值类型" width="130">
								<template #default="scope">
									<el-select v-model="scope.row.valueType">
										<el-option v-for="item in valueTypes" :key="item" :label="item" :value="item" />
									</el-select>
								</template>
							</el-table-column>
							<el-table-column label="单位" width="120">
								<template #default="scope">
									<el-input v-model="scope.row.unit" />
								</template>
							</el-table-column>
							<el-table-column label="启用" width="90">
								<template #default="scope">
									<el-switch v-model="scope.row.enabled" />
								</template>
							</el-table-column>
							<el-table-column label="操作" width="230" fixed="right">
								<template #default="scope">
									<el-button size="small" text type="primary" @click="openTransformDialog(scope.row)">转换</el-button>
									<el-button size="small" text @click="copyPoint(scope.$index)">复制</el-button>
									<el-button size="small" text type="danger" @click="removePoint(scope.$index)">删除</el-button>
								</template>
							</el-table-column>
						</el-table>
					</el-tab-pane>
				</el-tabs>
			</el-form>
			<template #footer>
				<el-button @click="dialogVisible = false">取消</el-button>
				<el-button type="primary" @click="submitForm">保存</el-button>
			</template>
		</el-dialog>

		<el-dialog v-model="transformDialogVisible" title="转换规则" width="760px" destroy-on-close>
			<div class="collection-task-page__table-toolbar">
				<el-button type="primary" @click="addTransformRule">新增规则</el-button>
			</div>
			<el-table :data="transformRules" row-key="_rowId" empty-text="暂无转换规则">
				<el-table-column label="规则" width="150">
					<template #default="scope">
						<el-select v-model="scope.row.transformType">
							<el-option label="倍率" value="Scale" />
							<el-option label="偏移" value="Offset" />
							<el-option label="小数位" value="Round" />
						</el-select>
					</template>
				</el-table-column>
				<el-table-column label="参数名" width="160">
					<template #default="scope">
						<el-input v-model="scope.row.parameterKey" placeholder="factor/value/digits" />
					</template>
				</el-table-column>
				<el-table-column label="参数值" min-width="160">
					<template #default="scope">
						<el-input-number v-model="scope.row.parameterValue" style="width: 100%" />
					</template>
				</el-table-column>
				<el-table-column label="操作" width="170">
					<template #default="scope">
						<el-button size="small" text @click="moveTransformRule(scope.$index, -1)">上移</el-button>
						<el-button size="small" text @click="moveTransformRule(scope.$index, 1)">下移</el-button>
						<el-button size="small" text type="danger" @click="removeTransformRule(scope.$index)">删除</el-button>
					</template>
				</el-table-column>
			</el-table>
			<template #footer>
				<el-button @click="transformDialogVisible = false">取消</el-button>
				<el-button type="primary" @click="saveTransformRules">保存</el-button>
			</template>
		</el-dialog>

		<el-dialog v-model="logDialogVisible" title="采集日志" width="1120px" destroy-on-close>
			<div class="collection-task-page__filters is-dialog">
				<el-select v-model="logQuery.status" placeholder="状态" clearable @change="loadLogs">
					<el-option label="Success" value="Success" />
					<el-option label="Timeout" value="Timeout" />
					<el-option label="CrcError" value="CrcError" />
					<el-option label="ParseError" value="ParseError" />
				</el-select>
				<el-date-picker
					v-model="logQuery.timeRange"
					type="datetimerange"
					range-separator="至"
					start-placeholder="开始时间"
					end-placeholder="结束时间"
					value-format="YYYY-MM-DDTHH:mm:ss"
					@change="loadLogs"
				/>
				<el-button type="primary" @click="loadLogs">查询</el-button>
			</div>
			<el-table :data="logs.rows" v-loading="logs.loading" empty-text="暂无采集日志">
				<el-table-column prop="requestAt" label="请求时间" min-width="180" />
				<el-table-column prop="responseAt" label="响应时间" min-width="180" />
				<el-table-column prop="status" label="状态" width="120">
					<template #default="scope">
						<el-tag :type="getLogStatusType(scope.row.status)">{{ scope.row.status || '--' }}</el-tag>
					</template>
				</el-table-column>
				<el-table-column prop="durationMs" label="耗时(ms)" width="110" />
				<el-table-column prop="requestFrame" label="请求帧" min-width="220" show-overflow-tooltip />
				<el-table-column prop="responseFrame" label="响应帧" min-width="220" show-overflow-tooltip />
				<el-table-column prop="parsedValue" label="解析值" min-width="120" show-overflow-tooltip />
				<el-table-column prop="convertedValue" label="转换值" min-width="120" show-overflow-tooltip />
				<el-table-column prop="errorMessage" label="错误" min-width="180" show-overflow-tooltip />
			</el-table>
			<div class="collection-task-page__pagination">
				<el-pagination
					v-model:current-page="logs.page"
					v-model:page-size="logs.pageSize"
					:page-sizes="[20, 50, 100]"
					layout="total, sizes, prev, pager, next"
					:total="logs.total"
					@current-change="loadLogs"
					@size-change="loadLogs"
				/>
			</div>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup>
import { computed, nextTick, onMounted, reactive, ref } from 'vue';
import type { FormInstance, FormRules } from 'element-plus';
import { ElMessage, ElMessageBox } from 'element-plus';
import { collectionTaskApi } from '/@/api/collectiontask';

type EditorPoint = {
	_rowId: string;
	pointKey: string;
	pointName: string;
	sourceType: string;
	address: number;
	rawValueType: string;
	length: number;
	byteOrder: string;
	wordOrder: string;
	readPeriodMs: number;
	pollingGroup: string;
	targetType: string;
	targetName: string;
	valueType: string;
	displayName: string;
	unit: string;
	group: string;
	enabled: boolean;
	transforms: EditorTransform[];
};

type EditorDevice = {
	_rowId: string;
	deviceKey: string;
	deviceName: string;
	slaveId: number;
	enabled: boolean;
	points: EditorPoint[];
};

type EditorTransform = {
	_rowId: string;
	transformType: string;
	parameterKey: string;
	parameterValue: number;
};

const api = collectionTaskApi();
const rawValueTypes = ['bool', 'int16', 'uint16', 'int32', 'uint32', 'float32', 'float64', 'string'];
const byteOrders = ['AB', 'BA', 'ABCD', 'CDAB', 'DCBA', 'BADC'];
const valueTypes = ['Boolean', 'Int32', 'Int64', 'Double', 'Decimal', 'String', 'Enum', 'Json'];

const state = reactive({
	tableData: {
		rows: [] as any[],
		total: 0,
		loading: false,
	},
});

const query = reactive({
	taskKey: '',
	gateway: '',
	protocol: '',
	enabled: '',
});

const pagination = reactive({
	page: 1,
	pageSize: 10,
});

const dialogVisible = ref(false);
const dialogTitle = ref('新建采集任务');
const activeEditorTab = ref('base');
const editingId = ref('');
const selectedDeviceIndex = ref(-1);
const formRef = ref<FormInstance>();
const form = reactive<any>(createEmptyForm());

const transformDialogVisible = ref(false);
const editingPoint = ref<EditorPoint | null>(null);
const transformRules = ref<EditorTransform[]>([]);

const logDialogVisible = ref(false);
const currentLogTask = ref<any>(null);
const logQuery = reactive({
	status: '',
	timeRange: [] as string[],
});
const logs = reactive({
	rows: [] as any[],
	total: 0,
	page: 1,
	pageSize: 50,
	loading: false,
});

const formRules: FormRules = {
	taskKey: [{ required: true, message: '请输入任务标识', trigger: 'blur' }],
	edgeNodeId: [{ required: true, message: '请输入网关设备ID', trigger: 'blur' }],
	protocol: [{ required: true, message: '请选择协议', trigger: 'change' }],
	'connection.connectionName': [{ required: true, message: '请输入连接名称', trigger: 'blur' }],
};

const filteredRows = computed(() => {
	return state.tableData.rows.filter((item) => {
		const matchesTask = !query.taskKey || item.taskKey?.toLowerCase().includes(query.taskKey.toLowerCase());
		const matchesGateway = !query.gateway || item.edgeNodeId?.toLowerCase().includes(query.gateway.toLowerCase());
		const matchesProtocol = !query.protocol || item.protocol === query.protocol;
		const matchesEnabled = query.enabled === '' || String(Boolean(item.enabled)) === query.enabled;
		return matchesTask && matchesGateway && matchesProtocol && matchesEnabled;
	});
});

const pagedRows = computed(() => {
	const start = (pagination.page - 1) * pagination.pageSize;
	return filteredRows.value.slice(start, start + pagination.pageSize);
});

const selectedPoints = computed(() => {
	if (selectedDeviceIndex.value < 0 || !form.devices[selectedDeviceIndex.value]) {
		return [];
	}
	return form.devices[selectedDeviceIndex.value].points;
});

function newRowId(prefix: string) {
	return `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function createEmptyForm() {
	return {
		id: '',
		taskKey: '',
		protocol: 'Modbus',
		version: 1,
		edgeNodeId: '',
		enabled: true,
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
		devices: [] as EditorDevice[],
		reportPolicy: {
			defaultTrigger: 'OnChange',
			includeQuality: true,
			includeTimestamp: true,
		},
	};
}

function createDevice(seed?: Partial<EditorDevice>): EditorDevice {
	return {
		_rowId: newRowId('device'),
		deviceKey: seed?.deviceKey || `slave-${form.devices.length + 1}`,
		deviceName: seed?.deviceName || '',
		slaveId: seed?.slaveId || 1,
		enabled: seed?.enabled ?? true,
		points: seed?.points || [],
	};
}

function createPoint(seed?: Partial<EditorPoint>): EditorPoint {
	return {
		_rowId: newRowId('point'),
		pointKey: seed?.pointKey || '',
		pointName: seed?.pointName || '',
		sourceType: seed?.sourceType || 'HoldingRegister',
		address: seed?.address ?? 0,
		rawValueType: seed?.rawValueType || 'float32',
		length: seed?.length || 2,
		byteOrder: seed?.byteOrder || 'ABCD',
		wordOrder: seed?.wordOrder || 'AB',
		readPeriodMs: seed?.readPeriodMs || 10000,
		pollingGroup: seed?.pollingGroup || 'default',
		targetType: seed?.targetType || 'Telemetry',
		targetName: seed?.targetName || '',
		valueType: seed?.valueType || 'Double',
		displayName: seed?.displayName || '',
		unit: seed?.unit || '',
		group: seed?.group || 'default',
		enabled: seed?.enabled ?? true,
		transforms: seed?.transforms || [],
	};
}

function toEditorDevice(device: any): EditorDevice {
	return createDevice({
		deviceKey: device.deviceKey || '',
		deviceName: device.deviceName || '',
		slaveId: Number(getSlaveId(device, 1)),
		enabled: device.enabled ?? true,
		points: (device.points || []).map(toEditorPoint),
	});
}

function getSlaveId(device: any, fallback: number | string = '--') {
	const protocolOptions = normalizeProtocolOptions(device?.protocolOptions);
	return device?.slaveId ?? protocolOptions?.SlaveId ?? protocolOptions?.slaveId ?? fallback;
}

function normalizeProtocolOptions(protocolOptions: any) {
	if (!protocolOptions || typeof protocolOptions !== 'string') {
		return protocolOptions;
	}
	try {
		return JSON.parse(protocolOptions);
	} catch {
		return {};
	}
}

function toEditorPoint(point: any): EditorPoint {
	return createPoint({
		pointKey: point.pointKey || '',
		pointName: point.pointName || '',
		sourceType: point.sourceType || 'HoldingRegister',
		address: Number(point.address ?? 0),
		rawValueType: point.rawValueType || 'float32',
		length: Number(point.length ?? 2),
		byteOrder: point.protocolOptions?.ByteOrder || point.protocolOptions?.byteOrder || 'ABCD',
		wordOrder: point.protocolOptions?.WordOrder || point.protocolOptions?.wordOrder || 'AB',
		readPeriodMs: Number(point.polling?.readPeriodMs ?? 10000),
		pollingGroup: point.polling?.group || 'default',
		targetType: point.mapping?.targetType || 'Telemetry',
		targetName: point.mapping?.targetName || '',
		valueType: point.mapping?.valueType || 'Double',
		displayName: point.mapping?.displayName || point.pointName || '',
		unit: point.mapping?.unit || '',
		group: point.mapping?.group || 'default',
		enabled: point.enabled ?? true,
		transforms: (point.transforms || []).map((item: any) => ({
			_rowId: newRowId('transform'),
			transformType: item.transformType || item.type || 'Scale',
			parameterKey: Object.keys(item.parameters || {})[0] || getDefaultTransformParameter(item.transformType || item.type || 'Scale'),
			parameterValue: Number(Object.values(item.parameters || {})[0] ?? 1),
		})),
	});
}

function toPayload() {
	const payload = {
		...form,
		connection: {
			...form.connection,
			protocol: form.protocol,
		},
		devices: form.devices.map((device: EditorDevice) => ({
			deviceKey: device.deviceKey,
			deviceName: device.deviceName,
			enabled: device.enabled,
			protocolOptions: {
				SlaveId: device.slaveId,
			},
			points: device.points.map((point) => ({
				pointKey: point.pointKey,
				pointName: point.pointName,
				sourceType: point.sourceType,
				address: String(point.address),
				rawValueType: point.rawValueType,
				length: point.length,
				enabled: point.enabled,
				polling: {
					readPeriodMs: point.readPeriodMs,
					group: point.pollingGroup,
				},
				protocolOptions: {
					ByteOrder: point.byteOrder,
					WordOrder: point.wordOrder,
				},
				transforms: point.transforms.map((rule, index) => ({
					transformType: rule.transformType,
					order: index + 1,
					parameters: {
						[rule.parameterKey || getDefaultTransformParameter(rule.transformType)]: Number(rule.parameterValue),
					},
				})),
				mapping: {
					targetType: point.targetType,
					targetName: point.targetName,
					valueType: point.valueType,
					displayName: point.displayName || point.pointName,
					unit: point.unit,
					group: point.group,
				},
			})),
		})),
	};

	if (!editingId.value) {
		delete payload.id;
	}

	return payload;
}

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

function handleSearch() {
	pagination.page = 1;
}

function resetFilters() {
	query.taskKey = '';
	query.gateway = '';
	query.protocol = '';
	query.enabled = '';
	handleSearch();
}

function openCreateDialog() {
	editingId.value = '';
	dialogTitle.value = '新建采集任务';
	activeEditorTab.value = 'base';
	Object.assign(form, createEmptyForm());
	form.devices.push(createDevice());
	selectedDeviceIndex.value = 0;
	dialogVisible.value = true;
	nextTick(() => formRef.value?.clearValidate());
}

function openEditDialog(row: any) {
	editingId.value = row.id;
	dialogTitle.value = '编辑采集任务';
	activeEditorTab.value = 'base';
	Object.assign(form, {
		...createEmptyForm(),
		...JSON.parse(JSON.stringify(row)),
		connection: {
			...createEmptyForm().connection,
			...(row.connection || {}),
		},
		reportPolicy: {
			...createEmptyForm().reportPolicy,
			...(row.reportPolicy || {}),
		},
		devices: (row.devices || []).map(toEditorDevice),
	});
	if (form.devices.length === 0) {
		form.devices.push(createDevice());
	}
	selectedDeviceIndex.value = 0;
	dialogVisible.value = true;
	nextTick(() => formRef.value?.clearValidate());
}

function addDevice() {
	form.devices.push(createDevice());
	selectedDeviceIndex.value = form.devices.length - 1;
}

function selectDevice(index: number) {
	selectedDeviceIndex.value = index;
	activeEditorTab.value = 'points';
}

function copyDevice(index: number) {
	const source = form.devices[index];
	if (!source) {
		return;
	}
	const copied = createDevice({
		...JSON.parse(JSON.stringify(source)),
		deviceKey: `${source.deviceKey}-copy`,
		deviceName: `${source.deviceName || source.deviceKey} 副本`,
		points: source.points.map((point: EditorPoint) => createPoint({
			...point,
			pointKey: `${point.pointKey}-copy`,
			transforms: point.transforms.map((rule) => ({ ...rule, _rowId: newRowId('transform') })),
		})),
	});
	form.devices.splice(index + 1, 0, copied);
	selectedDeviceIndex.value = index + 1;
}

async function removeDevice(index: number) {
	const device = form.devices[index];
	if (!device) {
		return;
	}
	if (device.points.length > 0) {
		await ElMessageBox.confirm('删除从站会同步删除点位配置，确定继续？', '提示', { type: 'warning' });
	}
	form.devices.splice(index, 1);
	selectedDeviceIndex.value = form.devices.length ? Math.min(index, form.devices.length - 1) : -1;
}

function addPoint() {
	if (selectedDeviceIndex.value < 0) {
		ElMessage.warning('请先选择从站');
		return;
	}
	form.devices[selectedDeviceIndex.value].points.push(createPoint());
}

function copyPoint(index: number) {
	const point = selectedPoints.value[index];
	if (!point || selectedDeviceIndex.value < 0) {
		return;
	}
	form.devices[selectedDeviceIndex.value].points.splice(index + 1, 0, createPoint({
		...JSON.parse(JSON.stringify(point)),
		pointKey: `${point.pointKey}-copy`,
		transforms: point.transforms.map((rule) => ({ ...rule, _rowId: newRowId('transform') })),
	}));
}

function removePoint(index: number) {
	if (selectedDeviceIndex.value < 0) {
		return;
	}
	form.devices[selectedDeviceIndex.value].points.splice(index, 1);
}

function batchSetPointEnabled(enabled: boolean) {
	selectedPoints.value.forEach((point) => {
		point.enabled = enabled;
	});
}

function openTransformDialog(point: EditorPoint) {
	editingPoint.value = point;
	transformRules.value = point.transforms.map((rule) => ({ ...rule, _rowId: newRowId('transform') }));
	transformDialogVisible.value = true;
}

function getDefaultTransformParameter(transformType: string) {
	if (transformType === 'Offset') {
		return 'value';
	}
	if (transformType === 'Round') {
		return 'digits';
	}
	return 'factor';
}

function addTransformRule() {
	transformRules.value.push({
		_rowId: newRowId('transform'),
		transformType: 'Scale',
		parameterKey: 'factor',
		parameterValue: 1,
	});
}

function moveTransformRule(index: number, offset: number) {
	const target = index + offset;
	if (target < 0 || target >= transformRules.value.length) {
		return;
	}
	const [item] = transformRules.value.splice(index, 1);
	transformRules.value.splice(target, 0, item);
}

function removeTransformRule(index: number) {
	transformRules.value.splice(index, 1);
}

function saveTransformRules() {
	if (!editingPoint.value) {
		return;
	}
	editingPoint.value.transforms = transformRules.value.map((rule) => ({ ...rule, _rowId: newRowId('transform') }));
	transformDialogVisible.value = false;
}

function validateEditorRows() {
	if (form.devices.length === 0) {
		ElMessage.error('请至少新增一个从站');
		activeEditorTab.value = 'devices';
		return false;
	}

	for (const [deviceIndex, device] of form.devices.entries()) {
		if (!device.deviceKey?.trim()) {
			ElMessage.error(`第 ${deviceIndex + 1} 个从站缺少从站标识`);
			activeEditorTab.value = 'devices';
			return false;
		}
		if (device.slaveId < 1 || device.slaveId > 247) {
			ElMessage.error(`${device.deviceKey} 的 SlaveId 必须在 1-247`);
			activeEditorTab.value = 'devices';
			return false;
		}
		if (device.points.length === 0) {
			ElMessage.error(`${device.deviceKey} 至少需要一个点位`);
			selectedDeviceIndex.value = deviceIndex;
			activeEditorTab.value = 'points';
			return false;
		}
		for (const [pointIndex, point] of device.points.entries()) {
			if (!point.pointKey?.trim() || !point.pointName?.trim()) {
				ElMessage.error(`${device.deviceKey} 第 ${pointIndex + 1} 个点位缺少标识或名称`);
				selectedDeviceIndex.value = deviceIndex;
				activeEditorTab.value = 'points';
				return false;
			}
			if (!point.targetName?.trim()) {
				ElMessage.error(`${point.pointKey} 缺少目标遥测键`);
				selectedDeviceIndex.value = deviceIndex;
				activeEditorTab.value = 'points';
				return false;
			}
			if (point.readPeriodMs < 1000) {
				ElMessage.error(`${point.pointKey} 的轮询周期不能小于 1000ms`);
				selectedDeviceIndex.value = deviceIndex;
				activeEditorTab.value = 'points';
				return false;
			}
		}
	}

	return true;
}

async function submitForm() {
	const valid = await formRef.value?.validate().catch(() => false);
	if (!valid || !validateEditorRows()) {
		return;
	}

	const payload = toPayload();
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
	} catch { }
}

async function openLogDialog(row: any) {
	currentLogTask.value = row;
	logDialogVisible.value = true;
	logs.page = 1;
	logQuery.status = '';
	logQuery.timeRange = [];
	await loadLogs();
}

async function loadLogs() {
	if (!currentLogTask.value) {
		return;
	}
	logs.loading = true;
	try {
		const res = await api.getLogs({
			gatewayDeviceId: currentLogTask.value.edgeNodeId,
			status: logQuery.status || undefined,
			startTime: logQuery.timeRange?.[0],
			endTime: logQuery.timeRange?.[1],
			limit: logs.pageSize,
			offset: (logs.page - 1) * logs.pageSize,
		});
		if (res.code === 10000) {
			logs.rows = res.data?.rows || [];
			logs.total = Number(res.data?.total ?? logs.rows.length);
		} else {
			ElMessage.error(res.msg || '获取日志失败');
		}
	} finally {
		logs.loading = false;
	}
}

function getLogStatusType(status: string) {
	if (status === 'Success') {
		return 'success';
	}
	if (status === 'Timeout' || status === 'NoResponse') {
		return 'warning';
	}
	if (status === 'CrcError' || status === 'ParseError' || status === 'ExceptionResponse') {
		return 'danger';
	}
	return 'info';
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

.collection-task-page__content {
	padding: 20px 22px;
	border-radius: 8px;
	border: 1px solid rgba(226, 232, 240, 0.92);
	background: #fff;
}

.collection-task-page__filters {
	display: flex;
	flex-wrap: wrap;
	gap: 12px;
	margin-bottom: 18px;
}

.collection-task-page__filters.is-dialog {
	margin-bottom: 14px;
}

.collection-task-page__filters .el-input,
.collection-task-page__filters .el-select {
	width: 220px;
}

.collection-task-page__table-toolbar {
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 10px;
	margin-bottom: 12px;
}

.collection-task-page__pagination {
	display: flex;
	justify-content: flex-end;
	margin-top: 16px;
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

:deep(.collection-task-page__table),
:deep(.editor-table) {
	border-radius: 8px;
	overflow: hidden;
}

:deep(.collection-task-page__table th.el-table__cell),
:deep(.editor-table th.el-table__cell) {
	background: #f8fbff;
}

:deep(.point-editor-table .el-input-number .el-input__wrapper) {
	padding-left: 8px;
	padding-right: 8px;
}

@media (max-width: 767px) {
	.collection-task-page__content {
		padding: 14px;
	}

	.collection-task-page__filters .el-input,
	.collection-task-page__filters .el-select {
		width: 100%;
	}

	.collection-task-page__pagination {
		justify-content: flex-start;
		overflow-x: auto;
	}
}
</style>
