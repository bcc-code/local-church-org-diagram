# swagger_client.GroupMembersApi

All URIs are relative to *https://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_group_member**](GroupMembersApi.md#create_group_member) | **POST** /groups/{groupUid}/members | Create a group member
[**find_group_members**](GroupMembersApi.md#find_group_members) | **GET** /groups/{groupUid}/members | Find a group member
[**get_group_member**](GroupMembersApi.md#get_group_member) | **GET** /groups/{groupUid}/members/{memberUid} | Get group members
[**get_groups_members**](GroupMembersApi.md#get_groups_members) | **GET** /groups/members | Get Members of multiple groups
[**remove_group_member**](GroupMembersApi.md#remove_group_member) | **DELETE** /groups/{groupUid}/members/{memberUid} | Remove a group member
[**remove_group_member_by_person_uid**](GroupMembersApi.md#remove_group_member_by_person_uid) | **DELETE** /groups/{groupUid}/members | Remove a group member by person uid


# **create_group_member**
> WrappedGroupMember create_group_member(group_uid, group_member)

Create a group member

You need the groups#write scope to access this endpoint.

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
api_instance = swagger_client.GroupMembersApi(swagger_client.ApiClient(configuration))
group_uid = 'group_uid_example' # str | Group Uid
group_member = swagger_client.GroupMemberWrite() # GroupMemberWrite | GroupMember

try:
    # Create a group member
    api_response = api_instance.create_group_member(group_uid, group_member)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling GroupMembersApi->create_group_member: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **group_uid** | **str**| Group Uid | 
 **group_member** | [**GroupMemberWrite**](GroupMemberWrite.md)| GroupMember | 

### Return type

[**WrappedGroupMember**](WrappedGroupMember.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **find_group_members**
> WrappedWithMetaArrayGroupMember find_group_members(group_uid, limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort)

Find a group member

You need the groups#read scope to access this endpoint.

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
api_instance = swagger_client.GroupMembersApi(swagger_client.ApiClient(configuration))
group_uid = 'group_uid_example' # str | Group Uid
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)

try:
    # Find a group member
    api_response = api_instance.find_group_members(group_uid, limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling GroupMembersApi->find_group_members: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **group_uid** | **str**| Group Uid | 
 **limit** | **int**| Limit | [optional] 
 **offset** | **int**| Offset | [optional] 
 **page** | **int**| Page | [optional] 
 **filter** | **str**| Filters | [optional] 
 **fields** | **str**| Fields | [optional] 
 **sort** | **str**| Sort | [optional] 

### Return type

[**WrappedWithMetaArrayGroupMember**](WrappedWithMetaArrayGroupMember.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_group_member**
> WrappedGroupMember get_group_member(member_uid, group_uid, fields=fields)

Get group members

You need the groups#read scope to access this endpoint.

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
api_instance = swagger_client.GroupMembersApi(swagger_client.ApiClient(configuration))
member_uid = 'member_uid_example' # str | GroupMember Uid
group_uid = 'group_uid_example' # str | Group Uid
fields = 'fields_example' # str | Fields to select (optional)

try:
    # Get group members
    api_response = api_instance.get_group_member(member_uid, group_uid, fields=fields)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling GroupMembersApi->get_group_member: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **member_uid** | **str**| GroupMember Uid | 
 **group_uid** | **str**| Group Uid | 
 **fields** | **str**| Fields to select | [optional] 

### Return type

[**WrappedGroupMember**](WrappedGroupMember.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_groups_members**
> WrappedWithMetaArrayPerson get_groups_members(group_uids, limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)

Get Members of multiple groups

You need the groups#read scope to access this endpoint.

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
api_instance = swagger_client.GroupMembersApi(swagger_client.ApiClient(configuration))
group_uids = 'group_uids_example' # str | Group Uids
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get Members of multiple groups
    api_response = api_instance.get_groups_members(group_uids, limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling GroupMembersApi->get_groups_members: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **group_uids** | **str**| Group Uids | 
 **limit** | **int**| Limit | [optional] 
 **offset** | **int**| Offset | [optional] 
 **page** | **int**| Page | [optional] 
 **filter** | **str**| Filters | [optional] 
 **fields** | **str**| Fields | [optional] 
 **sort** | **str**| Sort | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedWithMetaArrayPerson**](WrappedWithMetaArrayPerson.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **remove_group_member**
> WrappedGroupMember remove_group_member(group_uid, member_uid)

Remove a group member

You need the groups#write scope to access this endpoint.

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
api_instance = swagger_client.GroupMembersApi(swagger_client.ApiClient(configuration))
group_uid = 'group_uid_example' # str | Group Uid
member_uid = 'member_uid_example' # str | GroupMember Uid

try:
    # Remove a group member
    api_response = api_instance.remove_group_member(group_uid, member_uid)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling GroupMembersApi->remove_group_member: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **group_uid** | **str**| Group Uid | 
 **member_uid** | **str**| GroupMember Uid | 

### Return type

[**WrappedGroupMember**](WrappedGroupMember.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **remove_group_member_by_person_uid**
> WrappedGroupMember remove_group_member_by_person_uid(group_uid, person_uid)

Remove a group member by person uid

You need the groups#write scope to access this endpoint.

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
api_instance = swagger_client.GroupMembersApi(swagger_client.ApiClient(configuration))
group_uid = 'group_uid_example' # str | Group Uid
person_uid = 'person_uid_example' # str | Person Uid

try:
    # Remove a group member by person uid
    api_response = api_instance.remove_group_member_by_person_uid(group_uid, person_uid)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling GroupMembersApi->remove_group_member_by_person_uid: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **group_uid** | **str**| Group Uid | 
 **person_uid** | **str**| Person Uid | 

### Return type

[**WrappedGroupMember**](WrappedGroupMember.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

