# swagger_client.AffiliationsApi

All URIs are relative to *https://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_affiliation**](AffiliationsApi.md#create_affiliation) | **POST** /affiliations | Create affiliation
[**find_affiliations**](AffiliationsApi.md#find_affiliations) | **GET** /affiliations | Get affiliation
[**get_affiliation**](AffiliationsApi.md#get_affiliation) | **GET** /affiliations/{uid} | Get affiliation
[**remove_affiliation**](AffiliationsApi.md#remove_affiliation) | **DELETE** /affiliations/{uid} | Remove affiliation
[**update_affiliation**](AffiliationsApi.md#update_affiliation) | **PUT** /affiliations/{uid} | Update affiliation


# **create_affiliation**
> WrappedAffiliation create_affiliation(affiliation)

Create affiliation

You need the affiliations#write scope to access this endpoint.

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
api_instance = swagger_client.AffiliationsApi(swagger_client.ApiClient(configuration))
affiliation = swagger_client.AffiliationWrite() # AffiliationWrite | Affiliation

try:
    # Create affiliation
    api_response = api_instance.create_affiliation(affiliation)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling AffiliationsApi->create_affiliation: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **affiliation** | [**AffiliationWrite**](AffiliationWrite.md)| Affiliation | 

### Return type

[**WrappedAffiliation**](WrappedAffiliation.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **find_affiliations**
> WrappedWithMetaArrayAffiliation find_affiliations(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)

Get affiliation

You need the affiliations#read scope to access this endpoint.

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
api_instance = swagger_client.AffiliationsApi(swagger_client.ApiClient(configuration))
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get affiliation
    api_response = api_instance.find_affiliations(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling AffiliationsApi->find_affiliations: %s\n" % e)
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

[**WrappedWithMetaArrayAffiliation**](WrappedWithMetaArrayAffiliation.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_affiliation**
> WrappedAffiliation get_affiliation(uid, fields=fields, options=options)

Get affiliation

You need the affiliations#read scope to access this endpoint.

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
api_instance = swagger_client.AffiliationsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Affiliation Uid
fields = 'fields_example' # str | Fields to select (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get affiliation
    api_response = api_instance.get_affiliation(uid, fields=fields, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling AffiliationsApi->get_affiliation: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Affiliation Uid | 
 **fields** | **str**| Fields to select | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedAffiliation**](WrappedAffiliation.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **remove_affiliation**
> WrappedAffiliation remove_affiliation(uid)

Remove affiliation

You need the affiliations#write scope to access this endpoint.

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
api_instance = swagger_client.AffiliationsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Affiliation Uid

try:
    # Remove affiliation
    api_response = api_instance.remove_affiliation(uid)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling AffiliationsApi->remove_affiliation: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Affiliation Uid | 

### Return type

[**WrappedAffiliation**](WrappedAffiliation.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **update_affiliation**
> WrappedAffiliation update_affiliation(uid, affiliation)

Update affiliation

You need the affiliations#write scope to access this endpoint.

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
api_instance = swagger_client.AffiliationsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Affiliation Uid
affiliation = swagger_client.AffiliationWrite() # AffiliationWrite | Affiliation

try:
    # Update affiliation
    api_response = api_instance.update_affiliation(uid, affiliation)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling AffiliationsApi->update_affiliation: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Affiliation Uid | 
 **affiliation** | [**AffiliationWrite**](AffiliationWrite.md)| Affiliation | 

### Return type

[**WrappedAffiliation**](WrappedAffiliation.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

