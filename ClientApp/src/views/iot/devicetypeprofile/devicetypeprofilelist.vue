<template>
	<div class="device-type-profile-page">
		<ConsoleCrudWorkspace
			eyebrow="Device Type Profile"
			title="设备类型模板管理"
			description="为 HVAC 设备提供标准化的采集规则模板，支持快速初始化和数据映射。"
			card-eyebrow="Profile Table"
			card-title="设备类型模板列表"
			card-description="支持按设备类型筛选，管理采集规则模板和应用状态。"
			:badges="badges"
			:metrics="metrics"
		>
			<template #actions>
				<el-button type="primary" @click="getData">刷新列表</el-button>
				<el-button type="success" @click="openCreateDialog">新增模板</el-button>
			</template>

			<template #aside>
				<div class="device-type-profile-page__scope">
					<span>设备类型工作区</span>
					<strong>{{ state.tableData.total }}</strong>
					<small>标准化采集配置入口</small>
				</div>
			</template>

			<div class="device-type-profile-page__filters">
				<el-select v-model="query.deviceType" placeholder="选择设备类型" clearable @change="handleSearch">
					<el-option label="全部" :value="null" />
					<el-option label="冷水机组" :value="1" />
					<el-option label="热泵机组" :value="2" />
					<el-option label="水泵" :value="10" />
					<el-option label="冷却塔" :value="11" />
					<el-option label="风柜 (AHU)" :value="20" />
					<el-option label="风机盘管 (FCU)" :value="21" />
					<el-option label="阀门" :value="30" />
					<el-option label="电表" :value="40" />
					<el-option label="温度传感器" :value="50" />
					<el-option label="湿度传感器" :value="51" />
				</el-select>
			</div>

			<el-table :data="state.tableData.rows" row-key="id" v-loading="state.tableData.loading" class="device-type-profile-page__table">
				<el-table-column type="expand">
					<template #default="props">
						<div class="rules-container">
							<div class="rules-header">
								<span>采集规则模板</span>
								<el-button type="primary" size="small" @click="openRuleDialog(props.row.id)">
									<el-icon><Plus /></el-icon>
									添加规则
								</el-button>
							</div>
							<el-table :data="props.row.rules" size="small" class="rules-table">
								<el-table-column prop="pointKey" label="点位标识" width="150" />
								<el-table-column prop="pointName" label="显示名称" width="150" />
								<el-table-column prop="functionCode" label="功能码" width="100" />
								<el-table-column prop="address" label="地址" width="100" />
								<el-table-column prop="rawDataType" label="数据类型" width="100" />
								<el-table-column prop="byteOrder" label="字节序" width="100" />
								<el-table-column prop="readPeriodMs" label="周期(ms)" width="120" />
								<el-table-column prop="targetName" label="目标属性" width="150" />
								<el-table-column prop="unit" label="单位" width="80" />
								<el-table-column label="操作" width="150">
									<template #default="scope">
										<el-button size="small" text type="primary" @click="editRule(props.row.id, scope.row)">
											<el-icon><Edit /></el-icon>
										</el-button>
										<el-button size="small" text type="danger" @click="deleteRule(props.row.id, scope.row.id)">
											<el-icon><Delete /></el-icon>
										</el-button>
									</template>
								</el-table-column>
							</el-table>
						</div>
					</template>
				</el-table-column>

				<el-table-column prop="profileKey" label="模板标识" width="180">
					<template #default="scope">
						<div class="profile-key-cell">
							<strong>{{ scope.row.profileKey }}</strong>
						</div>
					</template>
				</el-table-column>

				<el-table-column prop="profileName" label="显示名称" min-width="180">
					<template #default="scope">
						<div class="profile-name-cell">
							<span>{{ scope.row.profileName }}</span>
							<el-tag v-if="scope.row.icon" size="small">{{ scope.row.icon }}</el-tag>
						</div>
					</template>
				</el-table-column>

				<el-table-column prop="deviceType" label="设备类型" width="140">
					<template #default="scope">
						<el-tag :type="getDeviceTypeTagType(scope.row.deviceType)">
							{{ getDeviceTypeName(scope.row.deviceType) }}
						</el-tag>
					</template>
				</el-table-column>

				<el-table-column prop="version" label="版本" width="80" />

				<el-table-column prop="description" label="描述" show-overflow-tooltip />

				<el-table-column label="规则数" width="100">
					<template #default="scope">
						<el-badge :value="scope.row.rules?.length || 0" type="primary" />
					</template>
				</el-table-column>

				<el-table-column label="状态" width="80">
					<template #default="scope">
						<el-tag :type="scope.row.enabled ? 'success' : 'info'" size="small">
							{{ scope.row.enabled ? '启用' : '禁用' }}
						</el-tag>
					</template>
				</el-table-column>

				<el-table-column label="操作" width="200">
					<template #default="scope">
						<el-button size="small" text type="primary" @click="openEditDialog(scope.row)">
							<el-icon><Edit /></el-icon>
							修改
						</el-button>
						<el-button size="small" text type="danger" @click="deleteProfile(scope.row.id)">
							<el-icon><Delete /></el-icon>
							删除
						</el-button>
					</template>
				</el-table-column>
			</el-table>
		</ConsoleCrudWorkspace>

		<!-- 模板表单对话框 -->
		<el-dialog v-model="dialogVisible" :title="dialogTitle" width="600px" destroy-on-close>
			<el-form :model="form" :rules="formRules" ref="formRef" label-width="120px">
				<el-form-item label="模板标识" prop="profileKey">
					<el-input v-model="form.profileKey" placeholder="如: chiller, water-pump" :disabled="isEdit" />
				</el-form-item>
				<el-form-item label="显示名称" prop="profileName">
					<el-input v-model="form.profileName" placeholder="如: 冷水机组" />
				</el-form-item>
				<el-form-item label="设备类型" prop="deviceType">
					<el-select v-model="form.deviceType" placeholder="选择设备类型" style="width: 100%">
						<el-option label="冷水机组" :value="1" />
						<el-option label="热泵机组" :value="2" />
						<el-option label="水泵" :value="10" />
						<el-option label="冷却塔" :value="11" />
						<el-option label="风柜 (AHU)" :value="20" />
						<el-option label="风机盘管 (FCU)" :value="21" />
						<el-option label="阀门" :value="30" />
						<el-option label="电表" :value="40" />
						<el-option label="温度传感器" :value="50" />
						<el-option label="湿度传感器" :value="51" />
					</el-select>
				</el-form-item>
				<el-form-item label="图标" prop="icon">
					<el-input v-model="form.icon" placeholder="可选: Chiller, Pump, Fan..." />
				</el-form-item>
				<el-form-item label="描述" prop="description">
					<el-input v-model="form.description" type="textarea" :rows="3" />
				</el-form-item>
				<el-form-item label="启用" prop="enabled">
					<el-switch v-model="form.enabled" />
				</el-form-item>
			</el-form>
			<template #footer>
				<el-button @click="dialogVisible = false">取消</el-button>
				<el-button type="primary" @click="submitForm">确定</el-button>
			</template>
		</el-dialog>

		<!-- 规则表单对话框 -->
		<el-dialog v-model="ruleDialogVisible" :title="ruleDialogTitle" width="700px" destroy-on-close>
			<el-form :model="ruleForm" :rules="ruleFormRules" ref="ruleFormRef" label-width="120px">
				<el-row :gutter="20">
					<el-col :span="12">
						<el-form-item label="点位标识" prop="pointKey">
							<el-input v-model="ruleForm.pointKey" placeholder="如: supply-temp" />
						</el-form-item>
					</el-col>
					<el-col :span="12">
						<el-form-item label="显示名称" prop="pointName">
							<el-input v-model="ruleForm.pointName" placeholder="如: 供水温度" />
						</el-form-item>
					</el-col>
				</el-row>
				<el-row :gutter="20">
					<el-col :span="12">
						<el-form-item label="功能码" prop="functionCode">
							<el-select v-model="ruleForm.functionCode" style="width: 100%">
								<el-option label="01 - 读线圈" :value="1" />
								<el-option label="02 - 读离散输入" :value="2" />
								<el-option label="03 - 读保持寄存器" :value="3" />
								<el-option label="04 - 读输入寄存器" :value="4" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :span="12">
						<el-form-item label="寄存器地址" prop="address">
							<el-input-number v-model="ruleForm.address" :min="0" :max="65535" style="width: 100%" />
						</el-form-item>
					</el-col>
				</el-row>
				<el-row :gutter="20">
					<el-col :span="12">
						<el-form-item label="数据类型" prop="rawDataType">
							<el-select v-model="ruleForm.rawDataType" style="width: 100%">
								<el-option label="uint16" :value="'uint16'" />
								<el-option label="int16" :value="'int16'" />
								<el-option label="uint32" :value="'uint32'" />
								<el-option label="int32" :value="'int32'" />
								<el-option label="float32" :value="'float32'" />
								<el-option label="float64" :value="'float64'" />
								<el-option label="bool" :value="'bool'" />
							</el-select>
						</el-form-item>
					</el-col>
					<el-col :span="12">
						<el-form-item label="字节序" prop="byteOrder">
							<el-select v-model="ruleForm.byteOrder" style="width: 100%">
								<el-option label="AB" value="AB" />
								<el-option label="BA" value="BA" />
								<el-option label="ABCD" value="ABCD" />
								<el-option label="CDAB" value="CDAB" />
								<el-option label="DCBA" value="DCBA" />
								<el-option label="BADC" value="BADC" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
				<el-row :gutter="20">
					<el-col :span="12">
						<el-form-item label="轮询周期(ms)" prop="readPeriodMs">
							<el-input-number v-model="ruleForm.readPeriodMs" :min="1000" :step="1000" style="width: 100%" />
						</el-form-item>
					</el-col>
					<el-col :span="12">
						<el-form-item label="单位" prop="unit">
							<el-input v-model="ruleForm.unit" placeholder="如: °C, kW" />
						</el-form-item>
					</el-col>
				</el-row>
				<el-row :gutter="20">
					<el-col :span="12">
						<el-form-item label="目标属性名" prop="targetName">
							<el-input v-model="ruleForm.targetName" placeholder="如: supplyTemperature" />
						</el-form-item>
					</el-col>
					<el-col :span="12">
						<el-form-item label="目标类型" prop="targetType">
							<el-select v-model="ruleForm.targetType" style="width: 100%">
								<el-option label="遥测 (Telemetry)" value="Telemetry" />
								<el-option label="属性 (Attribute)" value="Attribute" />
							</el-select>
						</el-form-item>
					</el-col>
				</el-row>
				<el-form-item label="换算规则" prop="transformsJson">
					<el-input v-model="ruleForm.transformsJson" type="textarea" :rows="2" placeholder='如: [{"type":"Scale","params":{"factor":0.1}}]' />
				</el-form-item>
				<el-form-item label="描述" prop="description">
					<el-input v-model="ruleForm.description" type="textarea" :rows="2" />
				</el-form-item>
			</el-form>
			<template #footer>
				<el-button @click="ruleDialogVisible = false">取消</el-button>
				<el-button type="primary" @click="submitRuleForm">确定</el-button>
			</template>
		</el-dialog>
	</div>
</template>

<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { Plus, Edit, Delete } from '@element-plus/icons-vue';
import ConsoleCrudWorkspace from '/@/components/console/ConsoleCrudWorkspace.vue';
import { deviceTypeProfileApi } from '/@/api/devicetypeprofile';

const api = deviceTypeProfileApi();

interface TableDataRow {
	id: string;
	profileKey: string;
	profileName: string;
	deviceType: number;
	description: string;
	icon: string;
	version: number;
	enabled: boolean;
	rules: Array<any>;
}

interface TableDataState {
	tableData: {
		rows: Array<TableDataRow>;
		total: number;
		loading: boolean;
	};
}

const state = reactive<TableDataState>({
	tableData: {
		rows: [],
		total: 0,
		loading: false,
	},
});

const query = reactive({
	deviceType: null as number | null,
});

const dialogVisible = ref(false);
const dialogTitle = ref('创建设备类型模板');
const isEdit = ref(false);
const formRef = ref();
const form = reactive({
	id: '',
	profileKey: '',
	profileName: '',
	deviceType: 1,
	description: '',
	icon: '',
	enabled: true,
});

const formRules = {
	profileKey: [{ required: true, message: '请输入模板标识', trigger: 'blur' }],
	profileName: [{ required: true, message: '请输入显示名称', trigger: 'blur' }],
	deviceType: [{ required: true, message: '请选择设备类型', trigger: 'change' }],
};

const ruleDialogVisible = ref(false);
const ruleDialogTitle = ref('添加采集规则');
const ruleFormRef = ref();
const currentProfileId = ref('');
const currentRuleId = ref('');
const ruleForm = reactive({
	pointKey: '',
	pointName: '',
	description: '',
	functionCode: 3,
	address: 0,
	registerCount: 1,
	rawDataType: 'uint16',
	byteOrder: 'AB',
	readPeriodMs: 30000,
	transformsJson: '',
	targetName: '',
	targetType: 'Telemetry',
	targetValueType: 'Double',
	unit: '',
	groupName: '',
	sortOrder: 0,
});

const ruleFormRules = {
	pointKey: [{ required: true, message: '请输入点位标识', trigger: 'blur' }],
	functionCode: [{ required: true, message: '请选择功能码', trigger: 'change' }],
	address: [{ required: true, message: '请输入寄存器地址', trigger: 'blur' }],
};

const badges = computed(() => [
	query.deviceType ? `类型: ${getDeviceTypeName(query.deviceType)}` : '全部类型',
	`共 ${state.tableData.total} 个模板`,
]);

const metrics = computed(() => [
	{ label: '模板总数', value: state.tableData.total, hint: '当前工作区内已创建的设备类型模板数量。', tone: 'primary' as const },
]);

const getDeviceTypeName = (type: number): string => {
	const map: Record<number, string> = {
		1: '冷水机组',
		2: '热泵机组',
		10: '水泵',
		11: '冷却塔',
		20: '风柜',
		21: '风机盘管',
		30: '阀门',
		40: '电表',
		50: '温度传感器',
		51: '湿度传感器',
	};
	return map[type] || `类型${type}`;
};

const getDeviceTypeTagType = (type: number): string => {
	const map: Record<number, string> = {
		1: 'primary',
		2: 'primary',
		10: 'success',
		11: 'success',
		20: 'warning',
		21: 'warning',
		30: 'info',
		40: 'danger',
		50: '',
		51: '',
	};
	return map[type] || 'info';
};

const getData = async () => {
	state.tableData.loading = true;
	try {
		const res = await api.getAll();
		if (res.code === 10000) {
			const payload = res.data;
			const rows = Array.isArray(payload)
				? payload
				: (payload?.rows || []);
			state.tableData.rows = query.deviceType
				? rows.filter((row: TableDataRow) => row.deviceType === query.deviceType)
				: rows;
			state.tableData.total = Array.isArray(payload)
				? rows.length
				: Number(payload?.total ?? rows.length);
		} else {
			ElMessage.error(res.msg || '获取数据失败');
		}
	} catch (e) {
		ElMessage.error('获取数据失败');
	} finally {
		state.tableData.loading = false;
	}
};

const handleSearch = () => {
	getData();
};

const openCreateDialog = () => {
	isEdit.value = false;
	dialogTitle.value = '创建设备类型模板';
	form.id = '';
	form.profileKey = '';
	form.profileName = '';
	form.deviceType = 1;
	form.description = '';
	form.icon = '';
	form.enabled = true;
	dialogVisible.value = true;
};

const openEditDialog = (row: TableDataRow) => {
	isEdit.value = true;
	dialogTitle.value = '编辑设备类型模板';
	form.id = row.id;
	form.profileKey = row.profileKey;
	form.profileName = row.profileName;
	form.deviceType = row.deviceType;
	form.description = row.description || '';
	form.icon = row.icon || '';
	form.enabled = row.enabled;
	dialogVisible.value = true;
};

const submitForm = async () => {
	if (!formRef.value) return;
	await formRef.value.validate(async (valid) => {
		if (valid) {
			try {
				let res;
				if (isEdit.value) {
					res = await api.update(form.id, form);
				} else {
					res = await api.create(form);
				}
				if (res.code === 10000) {
					ElMessage.success(isEdit.value ? '更新成功' : '创建成功');
					dialogVisible.value = false;
					getData();
				} else {
					ElMessage.error(res.msg || '操作失败');
				}
			} catch (e: any) {
				ElMessage.error(e.message || '操作失败');
			}
		}
	});
};

const deleteProfile = async (id: string) => {
	await ElMessageBox.confirm('确定删除该设备类型模板？', '警告', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	}).then(async () => {
		const res = await api.delete(id);
		if (res.code === 10000) {
			ElMessage.success('删除成功');
			getData();
		} else {
			ElMessage.error(res.msg || '删除失败');
		}
	}).catch(() => {});
};

const openRuleDialog = (profileId: string) => {
	currentProfileId.value = profileId;
	currentRuleId.value = '';
	ruleDialogTitle.value = '添加采集规则';
	ruleForm.pointKey = '';
	ruleForm.pointName = '';
	ruleForm.description = '';
	ruleForm.functionCode = 3;
	ruleForm.address = 0;
	ruleForm.registerCount = 1;
	ruleForm.rawDataType = 'uint16';
	ruleForm.byteOrder = 'AB';
	ruleForm.readPeriodMs = 30000;
	ruleForm.transformsJson = '';
	ruleForm.targetName = '';
	ruleForm.targetType = 'Telemetry';
	ruleForm.unit = '';
	ruleDialogVisible.value = true;
};

const editRule = (profileId: string, rule: any) => {
	currentProfileId.value = profileId;
	currentRuleId.value = rule.id;
	ruleDialogTitle.value = '编辑采集规则';
	ruleForm.pointKey = rule.pointKey;
	ruleForm.pointName = rule.pointName;
	ruleForm.description = rule.description || '';
	ruleForm.functionCode = rule.functionCode;
	ruleForm.address = rule.address;
	ruleForm.registerCount = rule.registerCount;
	ruleForm.rawDataType = rule.rawDataType;
	ruleForm.byteOrder = rule.byteOrder;
	ruleForm.readPeriodMs = rule.readPeriodMs;
	ruleForm.transformsJson = rule.transformsJson || '';
	ruleForm.targetName = rule.targetName || '';
	ruleForm.targetType = rule.targetType || 'Telemetry';
	ruleForm.unit = rule.unit || '';
	ruleDialogVisible.value = true;
};

const submitRuleForm = async () => {
	if (!ruleFormRef.value) return;
	await ruleFormRef.value.validate(async (valid) => {
		if (valid) {
			try {
				let res;
				if (currentRuleId.value) {
					res = await api.updateRule(currentProfileId.value, currentRuleId.value, ruleForm);
				} else {
					res = await api.addRule(currentProfileId.value, ruleForm);
				}
				if (res.code === 10000) {
					ElMessage.success(currentRuleId.value ? '更新成功' : '添加成功');
					ruleDialogVisible.value = false;
					getData();
				} else {
					ElMessage.error(res.msg || '操作失败');
				}
			} catch (e: any) {
				ElMessage.error(e.message || '操作失败');
			}
		}
	});
};

const deleteRule = async (profileId: string, ruleId: string) => {
	await ElMessageBox.confirm('确定删除该采集规则？', '警告', {
		confirmButtonText: '确定',
		cancelButtonText: '取消',
		type: 'warning',
	}).then(async () => {
		const res = await api.deleteRule(profileId, ruleId);
		if (res.code === 10000) {
			ElMessage.success('删除成功');
			getData();
		} else {
			ElMessage.error(res.msg || '删除失败');
		}
	}).catch(() => {});
};

onMounted(() => {
	getData();
});
</script>

<style lang="scss" scoped>
.device-type-profile-page {
	display: flex;
	flex-direction: column;
	gap: 18px;
}

.device-type-profile-page__scope {
	display: flex;
	flex-direction: column;
	align-items: flex-end;
	min-width: 170px;
	padding: 14px 16px;
	border-radius: 20px;
	border: 1px solid rgba(191, 219, 254, 0.9);
	background: rgba(255, 255, 255, 0.78);
}

.device-type-profile-page__scope span {
	color: #64748b;
	font-size: 12px;
}

.device-type-profile-page__scope strong {
	margin-top: 8px;
	color: #123b6d;
	font-size: 30px;
	line-height: 1;
}

.device-type-profile-page__scope small {
	margin-top: 8px;
	color: #7c8da1;
	font-size: 12px;
	line-height: 1.6;
	text-align: right;
}

.device-type-profile-page__filters {
	display: flex;
	align-items: center;
	flex-wrap: wrap;
	gap: 12px;
	margin-bottom: 18px;
}

.profile-key-cell strong {
	color: #123b6d;
	font-weight: 700;
}

.profile-name-cell {
	display: flex;
	align-items: center;
	gap: 8px;
}

.rules-container {
	padding: 16px;
	background: #f8fbff;
	border-radius: 8px;
}

.rules-header {
	display: flex;
	justify-content: space-between;
	align-items: center;
	margin-bottom: 12px;
	font-weight: 600;
	color: #123b6d;
}

.rules-table {
	background: white;
}

:deep(.device-type-profile-page__table) {
	border-radius: 20px;
	overflow: hidden;
}

:deep(.device-type-profile-page__table th.el-table__cell) {
	background: #f8fbff;
}
</style>
