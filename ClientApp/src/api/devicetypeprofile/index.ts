import request from '/@/utils/request';

/**
 * 设备类型模板 API
 */
export function deviceTypeProfileApi() {
	return {
		// 获取所有设备类型模板
		getAll: () => {
			return request({
				url: '/api/DeviceTypeProfile/GetAll',
				method: 'get',
			});
		},

		// 获取设备类型模板详情
		get: (id: string) => {
			return request({
				url: `/api/DeviceTypeProfile/Get/${id}`,
				method: 'get',
			});
		},

		// 创建设备类型模板
		create: (params: any) => {
			return request({
				url: '/api/DeviceTypeProfile/Create',
				method: 'post',
				data: params,
			});
		},

		// 更新设备类型模板
		update: (id: string, params: any) => {
			return request({
				url: `/api/DeviceTypeProfile/Update/${id}`,
				method: 'put',
				data: params,
			});
		},

		// 删除设备类型模板
		delete: (id: string) => {
			return request({
				url: `/api/DeviceTypeProfile/Delete/${id}`,
				method: 'delete',
			});
		},

		// 获取模板的采集规则
		getRules: (profileId: string) => {
			return request({
				url: `/api/DeviceTypeProfile/GetRules/${profileId}/rules`,
				method: 'get',
			});
		},

		// 添加采集规则模板
		addRule: (profileId: string, params: any) => {
			return request({
				url: `/api/DeviceTypeProfile/AddRule/${profileId}/rules`,
				method: 'post',
				data: params,
			});
		},

		// 更新采集规则模板
		updateRule: (profileId: string, ruleId: string, params: any) => {
			return request({
				url: `/api/DeviceTypeProfile/UpdateRule/${profileId}/rules/${ruleId}`,
				method: 'put',
				data: params,
			});
		},

		// 删除采集规则模板
		deleteRule: (profileId: string, ruleId: string) => {
			return request({
				url: `/api/DeviceTypeProfile/DeleteRule/${profileId}/rules/${ruleId}`,
				method: 'delete',
			});
		},

		// 应用设备类型模板到设备
		applyProfile: (deviceId: string, profileId: string) => {
			return request({
				url: '/api/DeviceTypeProfile/ApplyProfile',
				method: 'post',
				data: { deviceId, profileId },
			});
		},
	};
}
