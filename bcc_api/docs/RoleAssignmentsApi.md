# swagger_client.RoleAssignmentsApi

All URIs are relative to *https://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_role_assignment**](RoleAssignmentsApi.md#create_role_assignment) | **POST** /roleAssignments | Create role assignment
[**find_role_assignments**](RoleAssignmentsApi.md#find_role_assignments) | **GET** /roleAssignments | Get role assignment
[**get_role_assignment**](RoleAssignmentsApi.md#get_role_assignment) | **GET** /roleAssignments/{uid} | Get role assignment
[**remove_role_assignment**](RoleAssignmentsApi.md#remove_role_assignment) | **DELETE** /roleAssignments/{uid} | Remove role assignment
[**update_role_assignment**](RoleAssignmentsApi.md#update_role_assignment) | **PUT** /roleAssignments/{uid} | Update role assignment


# **create_role_assignment**
> WrappedRoleAssignment create_role_assignment(role_assignment)

Create role assignment

You need the persons.role_assignments#write scope to access this endpoint.

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
api_instance = swagger_client.RoleAssignmentsApi(swagger_client.ApiClient(configuration))
role_assignment = swagger_client.RoleAssignmentWrite() # RoleAssignmentWrite | RoleAssignment

try:
    # Create role assignment
    api_response = api_instance.create_role_assignment(role_assignment)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RoleAssignmentsApi->create_role_assignment: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **role_assignment** | [**RoleAssignmentWrite**](RoleAssignmentWrite.md)| RoleAssignment | 

### Return type

[**WrappedRoleAssignment**](WrappedRoleAssignment.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **find_role_assignments**
> WrappedWithMetaArrayRoleAssignment find_role_assignments(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)

Get role assignment

You need the persons.role_assignments#read scope to access this endpoint.

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
api_instance = swagger_client.RoleAssignmentsApi(swagger_client.ApiClient(configuration))
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get role assignment
    api_response = api_instance.find_role_assignments(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RoleAssignmentsApi->find_role_assignments: %s\n" % e)
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

[**WrappedWithMetaArrayRoleAssignment**](WrappedWithMetaArrayRoleAssignment.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_role_assignment**
> WrappedRoleAssignment get_role_assignment(uid, fields=fields, options=options)

Get role assignment

You need the persons.role_assignments#read scope to access this endpoint.

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
api_instance = swagger_client.RoleAssignmentsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | RoleAssignment Uid
fields = 'fields_example' # str | Fields to select (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get role assignment
    api_response = api_instance.get_role_assignment(uid, fields=fields, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RoleAssignmentsApi->get_role_assignment: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| RoleAssignment Uid | 
 **fields** | **str**| Fields to select | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedRoleAssignment**](WrappedRoleAssignment.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **remove_role_assignment**
> WrappedRoleAssignment remove_role_assignment(uid)

Remove role assignment

You need the persons.role_assignments#write scope to access this endpoint.

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
api_instance = swagger_client.RoleAssignmentsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | RoleAssignment Uid

try:
    # Remove role assignment
    api_response = api_instance.remove_role_assignment(uid)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RoleAssignmentsApi->remove_role_assignment: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| RoleAssignment Uid | 

### Return type

[**WrappedRoleAssignment**](WrappedRoleAssignment.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **update_role_assignment**
> WrappedRoleAssignment update_role_assignment(uid, role_assignment)

Update role assignment

You need the persons.role_assignments#write scope to access this endpoint.

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
api_instance = swagger_client.RoleAssignmentsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | RoleAssignment Uid
role_assignment = swagger_client.RoleAssignmentWrite() # RoleAssignmentWrite | RoleAssignment

try:
    # Update role assignment
    api_response = api_instance.update_role_assignment(uid, role_assignment)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling RoleAssignmentsApi->update_role_assignment: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| RoleAssignment Uid | 
 **role_assignment** | [**RoleAssignmentWrite**](RoleAssignmentWrite.md)| RoleAssignment | 

### Return type

[**WrappedRoleAssignment**](WrappedRoleAssignment.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

