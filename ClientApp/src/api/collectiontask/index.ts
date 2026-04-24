import request from '/@/utils/request';

export function collectionTaskApi() {
	return {
		getAll: () => request({
			url: '/api/CollectionTask/GetAll',
			method: 'get',
		}),
		get: (id: string) => request({
			url: `/api/CollectionTask/Get/${id}`,
			method: 'get',
		}),
		create: (params: any) => request({
			url: '/api/CollectionTask/Create',
			method: 'post',
			data: params,
		}),
		update: (id: string, params: any) => request({
			url: `/api/CollectionTask/Update/${id}`,
			method: 'put',
			data: params,
		}),
		delete: (id: string) => request({
			url: `/api/CollectionTask/Delete/${id}`,
			method: 'delete',
		}),
		enable: (id: string) => request({
			url: `/api/CollectionTask/Enable/${id}/Enable`,
			method: 'post',
		}),
		disable: (id: string) => request({
			url: `/api/CollectionTask/Disable/${id}/Disable`,
			method: 'post',
		}),
		getLogs: (params: any) => request({
			url: '/api/CollectionTask/GetLogs',
			method: 'get',
			params,
		}),
		getDraft: (protocol: string) => request({
			url: '/api/CollectionTask/GetDraft',
			method: 'get',
			params: { protocol },
		}),
		validateDraft: (params: any) => request({
			url: '/api/CollectionTask/ValidateDraft',
			method: 'post',
			data: params,
		}),
		preview: (params: any) => request({
			url: '/api/CollectionTask/Preview',
			method: 'post',
			data: params,
		}),
	};
}
