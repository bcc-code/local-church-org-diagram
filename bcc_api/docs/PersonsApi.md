# swagger_client.PersonsApi

All URIs are relative to *https://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**check_group_memberships**](PersonsApi.md#check_group_memberships) | **POST** /v2/persons/{personUid}/checkGroupMemberships | Check group memberships for a person
[**check_group_memberships_person**](PersonsApi.md#check_group_memberships_person) | **POST** /v2/person/{personUid}/checkGroupMemberships | Check group memberships for a person (legacy endpoint)
[**create_persons**](PersonsApi.md#create_persons) | **POST** /v2/persons | Create person
[**find_persons**](PersonsApi.md#find_persons) | **GET** /v2/persons | Find person
[**get_person**](PersonsApi.md#get_person) | **GET** /v2/persons/{uid} | Get person
[**get_person_id**](PersonsApi.md#get_person_id) | **GET** /v2/persons/personID/{uid} | Get personID
[**remove_person**](PersonsApi.md#remove_person) | **DELETE** /v2/persons/{uid} | Remove person
[**update_person**](PersonsApi.md#update_person) | **PUT** /v2/persons/{uid} | Update person


# **check_group_memberships**
> WrappedCheckGroupMembershipsResponse check_group_memberships(person_uid, groups)

Check group memberships for a person

Checks which out of provided groups the person is member of

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
person_uid = 'person_uid_example' # str | Person Uid
groups = swagger_client.CheckGroupMembershipsRequest() # CheckGroupMembershipsRequest | Group

try:
    # Check group memberships for a person
    api_response = api_instance.check_group_memberships(person_uid, groups)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->check_group_memberships: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **person_uid** | **str**| Person Uid | 
 **groups** | [**CheckGroupMembershipsRequest**](CheckGroupMembershipsRequest.md)| Group | 

### Return type

[**WrappedCheckGroupMembershipsResponse**](WrappedCheckGroupMembershipsResponse.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **check_group_memberships_person**
> WrappedCheckGroupMembershipsResponse check_group_memberships_person(person_uid, groups)

Check group memberships for a person (legacy endpoint)

Checks which out of provided groups the person is member of

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
person_uid = 'person_uid_example' # str | Person Uid
groups = swagger_client.CheckGroupMembershipsRequest() # CheckGroupMembershipsRequest | Group

try:
    # Check group memberships for a person (legacy endpoint)
    api_response = api_instance.check_group_memberships_person(person_uid, groups)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->check_group_memberships_person: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **person_uid** | **str**| Person Uid | 
 **groups** | [**CheckGroupMembershipsRequest**](CheckGroupMembershipsRequest.md)| Group | 

### Return type

[**WrappedCheckGroupMembershipsResponse**](WrappedCheckGroupMembershipsResponse.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **create_persons**
> WrappedPerson create_persons(person)

Create person

You need the persons#write scope to access this endpoint.

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
person = swagger_client.PersonWrite() # PersonWrite | Person

try:
    # Create person
    api_response = api_instance.create_persons(person)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->create_persons: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **person** | [**PersonWrite**](PersonWrite.md)| Person | 

### Return type

[**WrappedPerson**](WrappedPerson.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **find_persons**
> WrappedWithMetaArrayPerson find_persons(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)

Find person

You need the persons#read scope to access this endpoint.

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)
options = 'options_example' # str | Options (optional)

try:
    # Find person
    api_response = api_instance.find_persons(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->find_persons: %s\n" % e)
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

[**WrappedWithMetaArrayPerson**](WrappedWithMetaArrayPerson.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_person**
> WrappedPerson get_person(uid, fields=fields, options=options)

Get person

Person retrieval is permitted through the use of scopes.

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Person Uid
fields = 'fields_example' # str | Fields to select (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get person
    api_response = api_instance.get_person(uid, fields=fields, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->get_person: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Person Uid | 
 **fields** | **str**| Fields to select | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedPerson**](WrappedPerson.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_person_id**
> WrappedInt get_person_id(uid)

Get personID

Lookup person id by uid.

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Person Uid

try:
    # Get personID
    api_response = api_instance.get_person_id(uid)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->get_person_id: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Person Uid | 

### Return type

[**WrappedInt**](WrappedInt.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **remove_person**
> WrappedPerson remove_person(uid)

Remove person

You need the persons#write scope to access this endpoint.

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Person Uid

try:
    # Remove person
    api_response = api_instance.remove_person(uid)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->remove_person: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Person Uid | 

### Return type

[**WrappedPerson**](WrappedPerson.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **update_person**
> WrappedPerson update_person(uid, person)

Update person

You need the persons#write scope to access this endpoint.

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
api_instance = swagger_client.PersonsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Person Uid
person = swagger_client.PersonWrite() # PersonWrite | Person

try:
    # Update person
    api_response = api_instance.update_person(uid, person)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling PersonsApi->update_person: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Person Uid | 
 **person** | [**PersonWrite**](PersonWrite.md)| Person | 

### Return type

[**WrappedPerson**](WrappedPerson.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

