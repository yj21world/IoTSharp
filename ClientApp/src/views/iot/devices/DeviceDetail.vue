<template>
    <div>
        <el-drawer v-model="drawer" :size="'78%'" class="device-detail-drawer" append-to-body destroy-on-close>
            <div class="device-detail">

                <section class="device-detail__tabs-wrap">
                    <el-tabs v-model="activeTabName" class="device-detail-tabs">
                        <el-tab-pane name="basic">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <Monitor />
                                    </el-icon>
                                    概览
                                </span>
                            </template>
                            <div class="detail-pane">
                                <div class="detail-grid">
                                    <article class="detail-card detail-card--main">
                                        <div class="detail-card__header">
                                            <div>
                                                <h3>设备基础信息</h3>
                                                <p>展示设备类型、认证方式、最近活动时间以及平台内标识信息。</p>
                                            </div>
                                        </div>
                                        <AdvancedKeyValue :obj="deviceRef" :config="columns"
                                            :hide-key-list="['owner', 'identityValue', 'tenantName', 'customerName', 'tenantId', 'customerId']"
                                            :label-width="160">
                                            <template #footer v-if="canDownloadCert">
                                                <div class="detail-cert-row">
                                                    <div class="detail-cert-row__label">证书下载</div>
                                                    <div class="detail-cert-row__value">
                                                        <el-button type="primary" plain @click="downloadCert">导出 X509
                                                            证书包</el-button>
                                                    </div>
                                                </div>
                                            </template>
                                        </AdvancedKeyValue>
                                    </article>

                                    <article class="detail-card">
                                        <div class="detail-card__header">
                                            <div>
                                                <h3>快速信息</h3>
                                                <p>当前设备在租户、客户和鉴权层面的关键信息。</p>
                                            </div>
                                        </div>
                                        <div class="quick-info-list">
                                            <div v-for="item in quickInfoItems" :key="item.label"
                                                class="quick-info-item">
                                                <span>{{ item.label }}</span>
                                                <strong>{{ item.value }}</strong>
                                            </div>
                                        </div>
                                    </article>
                                </div>

                            <article class="detail-card detail-card--mqtt" style="margin-top:18px">
                                <div class="detail-card__header">
                                    <div>
                                        <h3>MQTT 配置指引</h3>
                                        <p>设备通过 MQTT 协议连接平台，上报遥测和属性数据、接收属性查询与 RPC 指令。</p>
                                    </div>
                                </div>
                                <div class="mqtt-guide-grid">
                                    <div class="mqtt-section">
                                        <h4>连接参数</h4>
                                        <div class="mqtt-param-list">
                                            <div class="mqtt-param-item">
                                                <span class="mqtt-param-label">Broker 地址</span>
                                                <code class="mqtt-param-value">{{ mqttBrokerHost }}</code>
                                            </div>
                                            <div class="mqtt-param-item">
                                                <span class="mqtt-param-label">端口</span>
                                                <code class="mqtt-param-value">{{ mqttPort }}</code>
                                            </div>
                                            <div class="mqtt-param-item">
                                                <span class="mqtt-param-label">TLS</span>
                                                <span class="mqtt-param-value">{{ mqttTlsLabel }}</span>
                                            </div>
                                            <div class="mqtt-param-item">
                                                <span class="mqtt-param-label">Client ID</span>
                                                <code class="mqtt-param-value">{{ deviceRef?.name || '--' }}</code>
                                            </div>
                                            <div class="mqtt-param-item">
                                                <span class="mqtt-param-label">认证方式</span>
                                                <el-tag size="small" type="warning">{{ identityTypeText }}</el-tag>
                                            </div>
                                            <div class="mqtt-param-item" v-if="deviceRef?.identityType === 'AccessToken'">
                                                <span class="mqtt-param-label">Token（用作用户名）</span>
                                                <code class="mqtt-param-value mqtt-token">{{ deviceRef?.identityId || '--' }}</code>
                                            </div>
                                            <div class="mqtt-param-item" v-if="deviceRef?.identityType === 'DevicePassword'">
                                                <span class="mqtt-param-label">用户名</span>
                                                <code class="mqtt-param-value">{{ deviceRef?.identityId || '--' }}</code>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="mqtt-section">
                                        <h4>主题定义</h4>
                                        <p class="mqtt-section-hint">设备名称 <strong>{{ deviceRef?.name || '{devname}' }}</strong> 对应主题中的 <code>{devname}</code></p>
                                        <div class="mqtt-topic-list">
                                            <div class="mqtt-topic-item">
                                                <div class="mqtt-topic-label">上报遥测</div>
                                                <code class="mqtt-topic-code">{{ telemetryTopic }}</code>
                                                <span class="mqtt-topic-desc">Payload 为 JSON 键值对</span>
                                            </div>
                                            <div class="mqtt-topic-item">
                                                <div class="mqtt-topic-label">上报属性</div>
                                                <code class="mqtt-topic-code">{{ attributesTopic }}</code>
                                                <span class="mqtt-topic-desc">Payload 为 JSON 键值对</span>
                                            </div>
                                            <div class="mqtt-topic-item">
                                                <div class="mqtt-topic-label">属性请求</div>
                                                <code class="mqtt-topic-code">{{ attributesRequestTopic }}</code>
                                                <span class="mqtt-topic-desc">设备请求平台返回最新属性值</span>
                                            </div>
                                            <div class="mqtt-topic-item">
                                                <div class="mqtt-topic-label">属性响应</div>
                                                <code class="mqtt-topic-code">devices/me/attributes/response/{requestid}</code>
                                                <span class="mqtt-topic-desc">平台推送属性查询结果（需订阅）</span>
                                            </div>
                                            <div class="mqtt-topic-item">
                                                <div class="mqtt-topic-label">RPC 指令</div>
                                                <code class="mqtt-topic-code">{{ rpcTopic }}</code>
                                                <span class="mqtt-topic-desc">设备订阅以接收平台下发的 RPC 调用</span>
                                            </div>
                                            <template v-if="deviceRef?.deviceType === 'Gateway'">
                                                <div class="mqtt-topic-item">
                                                    <div class="mqtt-topic-label">Modbus 请求</div>
                                                    <code class="mqtt-topic-code">{{ modbusReqTopic }}</code>
                                                    <span class="mqtt-topic-desc">平台向网关下发采集指令</span>
                                                </div>
                                                <div class="mqtt-topic-item">
                                                    <div class="mqtt-topic-label">Modbus 响应</div>
                                                    <code class="mqtt-topic-code">{{ modbusRespTopic }}</code>
                                                    <span class="mqtt-topic-desc">网关向平台上传采集结果</span>
                                                </div>
                                            </template>
                                        </div>
                                    </div>
                                </div>
                            </article>

                            </div>
                        </el-tab-pane>

                        <el-tab-pane name="props">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <Grid />
                                    </el-icon>
                                    属性
                                </span>
                            </template>
                            <div class="detail-pane">
                                <DeviceDetailProps :device-id="deviceRef?.id" />
                            </div>
                        </el-tab-pane>

                        <el-tab-pane name="telemetry">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <DataAnalysis />
                                    </el-icon>
                                    遥测
                                </span>
                            </template>
                            <div class="detail-pane">
                                <DeviceDetailTelemetry :device-id="deviceRef?.id" />
                            </div>
                        </el-tab-pane>

                        <el-tab-pane name="telemetryHistory" :lazy="true">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <TrendCharts />
                                    </el-icon>
                                    遥测历史
                                </span>
                            </template>
                            <div class="detail-pane">
                                <DeviceDetailTelemetryHistory :device-id="deviceRef?.id" />
                            </div>
                        </el-tab-pane>

                        <el-tab-pane name="alarm">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <Bell />
                                    </el-icon>
                                    告警
                                </span>
                            </template>
                            <div class="detail-pane">
                                <alarmlist :originator="deviceRef" wrapper="div"></alarmlist>
                            </div>
                        </el-tab-pane>

                        <el-tab-pane name="rules">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <Connection />
                                    </el-icon>
                                    规则
                                </span>
                            </template>
                            <div class="detail-pane">
                                <DeviceDetailRules :device-id="deviceRef?.id" />
                            </div>
                        </el-tab-pane>

                        <el-tab-pane name="collectionTasks" :lazy="true">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <Connection />
                                    </el-icon>
                                    采集任务
                                </span>
                            </template>
                            <div class="detail-pane">
                                <CollectionTaskWorkspace :device-id="deviceRef?.id" embedded />
                            </div>
                        </el-tab-pane>

                        <el-tab-pane name="rulesHistory">
                            <template #label>
                                <span class="device-tab-label">
                                    <el-icon>
                                        <Clock />
                                    </el-icon>
                                    规则历史
                                </span>
                            </template>
                            <div class="detail-pane">
                                <flowevents :creator="deviceRef?.id" :creatorname="deviceRef?.name" wrapper="div">
                                </flowevents>
                            </div>
                        </el-tab-pane>


                    </el-tabs>
                </section>
            </div>
        </el-drawer>
    </div>
</template>

<script lang="ts" setup>
import dayjs from 'dayjs';
import { computed, ref } from 'vue';
import { ElMessage } from 'element-plus';
import {
    Bell,
    CircleCheckFilled,
    CircleCloseFilled,
    Clock,
    Connection,
    DataAnalysis,
    Download,
    Grid,
    Location,
    Monitor,
    Refresh,
    TrendCharts,
} from '@element-plus/icons-vue';
import { deviceDetailBaseInfoColumns } from '/@/views/iot/devices/detail/deviceDetailBaseInfoColumns';
import AdvancedKeyValue from '/@/components/AdvancedKeyValue/AdvancedKeyValue.vue';
import DeviceDetailProps from '/@/views/iot/devices/detail/DeviceDetailProps.vue';
import DeviceDetailRules from '/@/views/iot/devices/detail/DeviceDetailRules.vue';
import DeviceDetailTelemetry from '/@/views/iot/devices/detail/DeviceDetailTelemetry.vue';
import DeviceDetailTelemetryHistory from '/@/views/iot/devices/detail/DeviceDetailTelemetryHistory.vue';
import CollectionTaskWorkspace from '/@/views/iot/collectiontask/components/CollectionTaskWorkspace.vue';
import Alarmlist from '/@/views/iot/alarms/alarmlist.vue';
import Flowevents from '/@/views/iot/rules/flowevents.vue';
import { deviceApi } from '/@/api/devices';
import { downloadByData } from '/@/utils/download';
import { getSysInfo } from '/@/api/installer';
import BMap from '/@/views/iot/devices/detail/maps/bmap/BMap.vue';

const drawer = ref(false);
const activeTabName = ref('basic');
const deviceRef = ref<any>();
const refreshing = ref(false);
const columns = deviceDetailBaseInfoColumns;

const deviceName = computed(() => deviceRef.value?.name || '设备详情');
const canDownloadCert = computed(() => deviceRef.value?.identityType === 'X509Certificate');
const statusClass = computed(() => (deviceRef.value?.active ? 'online' : 'offline'));
const statusText = computed(() => (deviceRef.value?.active ? '活动中' : '静默'));
const deviceTypeText = computed(() => {
    if (deviceRef.value?.deviceType === 'Gateway') return '网关';
    if (deviceRef.value?.deviceType === 'Device') return '设备';
    return deviceRef.value?.deviceType || '--';
});
const identityTypeText = computed(() => {
    if (deviceRef.value?.identityType === 'AccessToken') return 'AccessToken';
    if (deviceRef.value?.identityType === 'X509Certificate') return 'X509 Certificate';
    return deviceRef.value?.identityType || '--';
});
const lastActivityText = computed(() => {
    const value = deviceRef.value?.lastActivityDateTime;
    if (!value) return '暂无记录';
    return dayjs(value).format('YYYY-MM-DD HH:mm:ss');
});

const summaryCards = computed(() => [
    {
        label: '设备 ID',
        value: deviceRef.value?.id || '--',
        hint: '平台内唯一标识',
    },
    {
        label: '连接状态',
        value: statusText.value,
        hint: `最近活动：${lastActivityText.value}`,
    },
    {
        label: '认证方式',
        value: identityTypeText.value,
        hint: deviceRef.value?.identityId || '暂无鉴权标识',
    },
]);

const quickInfoItems = computed(() => [
    { label: '租户', value: deviceRef.value?.tenantName || '--' },
    { label: '客户', value: deviceRef.value?.customerName || '--' },
    { label: '拥有者', value: deviceRef.value?.owner || '--' },
    { label: '超时设置', value: deviceRef.value?.timeout ? `${deviceRef.value.timeout}` : '--' },
]);

const instanceRef = ref<any>(null);

const mqttBrokerHost = computed(() => {
    const domain = instanceRef.value?.domain;
    if (!domain) return '（获取中...）';
    try {
        const url = new URL(domain);
        return url.hostname;
    } catch {
        return domain;
    }
});

const mqttPort = computed(() => {
    if (instanceRef.value?.enableTls) return '8883 (TLS)';
    return '1883';
});

const mqttTlsLabel = computed(() => {
    return instanceRef.value?.enableTls ? '已启用 (TLS 1.2)' : '未启用';
});

const telemetryTopic = computed(() =>
    `devices/${deviceRef.value?.name || '{devname}'}/Telemetry`
);
const attributesTopic = computed(() =>
    `devices/${deviceRef.value?.name || '{devname}'}/Attributes`
);
const attributesRequestTopic = computed(() =>
    `devices/${deviceRef.value?.name || '{devname}'}/Attributes/request/{requestid}`
);
const rpcTopic = computed(() =>
    `devices/${deviceRef.value?.name || '{devname}'}/Rpc/request/{method}`
);
const modbusReqTopic = computed(() =>
    `gateway/${deviceRef.value?.name || '{gw}'}/modbus/request/{id}`
);
const modbusRespTopic = computed(() =>
    `gateway/${deviceRef.value?.name || '{gw}'}/modbus/response`
);

const openDialog = (device: any) => {
    drawer.value = true;
    activeTabName.value = 'basic';
    deviceRef.value = Object.assign({}, device);
    void reloadbaseinfo();
    void loadInstanceInfo();
};

const reloadbaseinfo = async () => {
    if (!deviceRef.value?.id) return;
    refreshing.value = true;
    try {
        const response = await deviceApi().getdevcie(deviceRef.value.id);
        deviceRef.value = Object.assign({}, response.data);
    }
    catch (error) {
        ElMessage.error('设备基础信息刷新失败');
    }
    finally {
        refreshing.value = false;
    }
};

const downloadCert = async () => {
    if (!deviceRef.value?.id) return;
    const res = await deviceApi().downloadCertificates(deviceRef.value.id);
    downloadByData(res, `${deviceRef.value.id}.zip`);
};

const loadInstanceInfo = async () => {
    try {
        const res = await getSysInfo();
        instanceRef.value = res.data;
    } catch {
        // use defaults
    }
};

defineExpose({
    openDialog,
});
</script>

<style lang="scss" scoped>
:deep(.device-detail-drawer .el-drawer__header) {
    margin-bottom: 0;
    padding-bottom: 0;
}

:deep(.device-detail-drawer .el-drawer__body) {
    padding: 18px;
    background: linear-gradient(180deg, #f8fbff 0%, #f4f8fc 100%);
}

.device-detail {
    --device-detail-pane-height: calc(100vh - 420px);
    display: flex;
    flex-direction: column;
    gap: 18px;
}

.device-detail__hero,
.detail-card,
.device-detail__tabs-wrap {
    border: 1px solid rgba(148, 163, 184, 0.16);
    background:
        radial-gradient(circle at top right, rgba(14, 165, 233, 0.08), transparent 24%),
        linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(248, 250, 252, 0.94));
    box-shadow: 0 20px 45px rgba(15, 23, 42, 0.08);
}

.device-detail__hero {
    display: grid;
    grid-template-columns: 1.6fr 1fr;
    gap: 18px;
    padding: 24px;
    border-radius: 28px;
}

.device-detail__eyebrow {
    margin-bottom: 10px;
    color: #0f766e;
    font-size: 12px;
    font-weight: 700;
    letter-spacing: 0.18em;
    text-transform: uppercase;
}

.device-detail__title-row {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 18px;

    h2 {
        margin: 0 0 10px;
        color: #0f172a;
        font-size: 32px;
        letter-spacing: -0.04em;
    }

    p {
        color: #64748b;
        font-size: 14px;
        line-height: 1.8;
    }
}

.device-detail__hero-actions {
    display: flex;
    gap: 10px;
    flex-shrink: 0;
}

.device-detail__hero-meta {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
    margin-top: 22px;
}

.status-pill,
.hero-meta-item,
.summary-card,
.quick-info-item {
    padding: 14px 16px;
    border-radius: 18px;
    border: 1px solid rgba(148, 163, 184, 0.12);
    background: rgba(255, 255, 255, 0.8);
}

.status-pill {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    font-weight: 600;
}

.status-pill--online {
    color: #059669;
    background: rgba(16, 185, 129, 0.1);
}

.status-pill--offline {
    color: #dc2626;
    background: rgba(239, 68, 68, 0.1);
}

.hero-meta-item {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 140px;

    span {
        color: #64748b;
        font-size: 12px;
    }

    strong {
        color: #0f172a;
        font-size: 16px;
        font-weight: 700;
    }
}

.device-detail__hero-side {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.summary-card__label {
    color: #64748b;
    font-size: 12px;
}

.summary-card__value {
    margin: 10px 0 8px;
    color: #0f172a;
    font-size: 22px;
    font-weight: 700;
    word-break: break-all;
}

.summary-card p {
    color: #64748b;
    font-size: 12px;
    line-height: 1.7;
}

.device-detail__tabs-wrap {
    padding: 16px 18px;
    border-radius: 24px;
}

.device-tab-label {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    font-weight: 600;
}

.detail-pane {
    padding-top: 10px;
}

.detail-grid {
    display: grid;
    grid-template-columns: 2fr 1fr;
    gap: 18px;
}

.detail-card {
    padding: 20px;
    border-radius: 22px;
}

.detail-card--main {
    min-height: 100%;
}

.detail-card__header {
    margin-bottom: 14px;

    h3 {
        margin: 0 0 8px;
        color: #0f172a;
        font-size: 20px;
    }

    p {
        color: #64748b;
        font-size: 13px;
        line-height: 1.7;
    }
}

.detail-cert-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 12px;
    width: 100%;
    padding: 10px 16px 0;
}

.detail-cert-row__label {
    width: 160px;
    color: #64748b;
}

.detail-cert-row__value {
    flex: 1;
}

.quick-info-list {
    display: grid;
    grid-template-columns: repeat(1, minmax(0, 1fr));
    gap: 12px;
}

.quick-info-item {
    display: flex;
    flex-direction: column;
    gap: 8px;

    span {
        color: #64748b;
        font-size: 12px;
    }

    strong {
        color: #0f172a;
        font-size: 16px;
        word-break: break-all;
    }
}

/* ---- MQTT config guide ---- */
.mqtt-guide-grid {
    display: grid;
    grid-template-columns: 1fr 2fr;
    gap: 24px;
}

.mqtt-section {
    h4 {
        margin: 0 0 12px;
        color: #0f172a;
        font-size: 16px;
        font-weight: 600;
    }
}

.mqtt-section-hint {
    margin: 0 0 12px;
    color: #64748b;
    font-size: 13px;

    code {
        padding: 1px 6px;
        border-radius: 4px;
        background: rgba(14, 165, 233, 0.1);
        color: #0e7490;
        font-size: 12px;
    }
}

.mqtt-param-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.mqtt-param-item {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 12px;
    border-radius: 10px;
    background: rgba(248, 250, 252, 0.8);
}

.mqtt-param-label {
    min-width: 100px;
    color: #64748b;
    font-size: 13px;
    flex-shrink: 0;
}

.mqtt-param-value {
    color: #0f172a;
    font-weight: 600;
    font-size: 13px;
    word-break: break-all;
}

code.mqtt-param-value {
    padding: 2px 8px;
    border-radius: 4px;
    background: rgba(14, 165, 233, 0.08);
    color: #0e7490;
    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    font-weight: 500;
}

.mqtt-token {
    user-select: all;
    cursor: copy;
}

.mqtt-topic-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.mqtt-topic-item {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 12px;
    border-radius: 10px;
    background: rgba(248, 250, 252, 0.8);
    flex-wrap: wrap;
}

.mqtt-topic-label {
    min-width: 70px;
    color: #475569;
    font-size: 13px;
    font-weight: 600;
    flex-shrink: 0;
}

code.mqtt-topic-code {
    padding: 2px 8px;
    border-radius: 4px;
    background: rgba(14, 165, 233, 0.08);
    color: #0e7490;
    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    font-size: 12px;
    font-weight: 500;
    word-break: break-all;
}

.mqtt-topic-desc {
    color: #94a3b8;
    font-size: 12px;
    flex-shrink: 0;
}

@media (max-width: 1280px) {

    .device-detail__hero,
    .detail-grid,
    .mqtt-guide-grid {
        grid-template-columns: 1fr;
    }
}

@media (max-width: 767px) {
    :deep(.device-detail-drawer) {
        width: 100% !important;
    }

    :deep(.device-detail-drawer .el-drawer__body) {
        padding: 12px;
    }

    .device-detail {
        --device-detail-pane-height: calc(100vh - 480px);
    }

    .device-detail__hero,
    .device-detail__tabs-wrap {
        padding: 16px;
        border-radius: 20px;
    }

    .device-detail__title-row {
        flex-direction: column;
    }

    .device-detail__hero-actions {
        width: 100%;
    }

    .device-detail__hero-actions :deep(.el-button) {
        flex: 1;
    }

    .hero-meta-item {
        width: 100%;
    }
}
</style>
