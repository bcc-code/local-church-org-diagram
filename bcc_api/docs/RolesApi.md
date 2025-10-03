# swagger_client.RolesApi

All URIs are relative to *https://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_role**](RolesApi.md#create_role) | **POST** /roles | Create role
[**find_roles**](RolesApi.md#find_roles) | **GET** /roles | Find role
[**get_role**](RolesApi.md#get_role) | **GET** /roles/{uid} | Get role


# **create_role**
> WrappedRole create_role(role)

Create role

You need the roles#write scope to access this endpoint.

### Example
```python
from __future__ import print_function
import time
import swagger_client
from swagger_client.rest import ApiException
from pprint import pprint

# Configure OAuth2 access token for authorization: ClientCredentials
configuration = swagger_client.Configuration()
configuration.access_token = 'YOUR_ACCESS_TOKEN'

# create an instance of the API class
api_instance = swagger_client.RolesApi(swagger_client.ApiClient(configuration))
role = swagger_client.RoleWrite() # RoleWrite | Role

try:
    # Create role
    api_response = api_instance.create_role(role)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RolesApi->create_role: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **role** | [**RoleWrite**](RoleWrite.md)| Role | 

### Return type

[**WrappedRole**](WrappedRole.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **find_roles**
> WrappedWithMetaArrayRole find_roles(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)

Find role

You need the roles#read scope to access this endpoint.

### Example
```python
from __future__ import print_function
import time
import swagger_client
from swagger_client.rest import ApiException
from pprint import pprint

# Configure OAuth2 access token for authorization: ClientCredentials
configuration = swagger_client.Configuration()
configuration.access_token = 'YOUR_ACCESS_TOKEN'

# create an instance of the API class
api_instance = swagger_client.RolesApi(swagger_client.ApiClient(configuration))
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)
options = 'options_example' # str | Options (optional)

try:
    # Find role
    api_response = api_instance.find_roles(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RolesApi->find_roles: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **limit** | **int**| Limit | [optional] 
 **offset** | **int**| Offset | [optional] 
 **page** | **int**| Page | [optional] 
 **filter** | **str**| Filters | [optional] 
 **fields** | **str**| Fields | [optional] 
 **sort** | **str**| Sort | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedWithMetaArrayRole**](WrappedWithMetaArrayRole.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_role**
> WrappedRole get_role(uid, fields=fields, options=options)

Get role

You need the roles#read scope to access this endpoint.

### Example
```python
from __future__ import print_function
import time
import swagger_client
from swagger_client.rest import ApiException
from pprint import pprint

# Configure OAuth2 access token for authorization: ClientCredentials
configuration = swagger_client.Configuration()
configuration.access_token = 'YOUR_ACCESS_TOKEN'

# create an instance of the API class
api_instance = swagger_client.RolesApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Role Uid
fields = 'fields_example' # str | Fields to select (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get role
    api_response = api_instance.get_role(uid, fields=fields, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RolesApi->get_role: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Role Uid | 
 **fields** | **str**| Fields to select | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedRole**](WrappedRole.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

