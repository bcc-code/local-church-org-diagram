# swagger_client.ConsentsApi

All URIs are relative to *https://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_consent**](ConsentsApi.md#create_consent) | **POST** /consents | Create consent
[**find_consents**](ConsentsApi.md#find_consents) | **GET** /consents | Find consents
[**get_consent**](ConsentsApi.md#get_consent) | **GET** /consents/{uid} | Get consent
[**update_consent**](ConsentsApi.md#update_consent) | **PUT** /consents/{uid} | Update consent


# **create_consent**
> WrappedConsent create_consent(consent)

Create consent

You need the consents#write scope to access this endpoint.

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
api_instance = swagger_client.ConsentsApi(swagger_client.ApiClient(configuration))
consent = swagger_client.ConsentWrite() # ConsentWrite | Consent

try:
    # Create consent
    api_response = api_instance.create_consent(consent)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling ConsentsApi->create_consent: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **consent** | [**ConsentWrite**](ConsentWrite.md)| Consent | 

### Return type

[**WrappedConsent**](WrappedConsent.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **find_consents**
> WrappedWithMetaArrayConsent find_consents(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)

Find consents

You need the consents#read scope to access this endpoint.

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
api_instance = swagger_client.ConsentsApi(swagger_client.ApiClient(configuration))
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)
options = 'options_example' # str | Options (optional)

try:
    # Find consents
    api_response = api_instance.find_consents(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling ConsentsApi->find_consents: %s\n" % e)
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

[**WrappedWithMetaArrayConsent**](WrappedWithMetaArrayConsent.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_consent**
> WrappedConsent get_consent(uid, fields=fields, options=options)

Get consent

You need the consents#read scope to access this endpoint.

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
api_instance = swagger_client.ConsentsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Consent Uid
fields = 'fields_example' # str | Fields to select (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get consent
    api_response = api_instance.get_consent(uid, fields=fields, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling ConsentsApi->get_consent: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Consent Uid | 
 **fields** | **str**| Fields to select | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedConsent**](WrappedConsent.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **update_consent**
> WrappedConsent update_consent(uid, consent)

Update consent

You need the consents#write scope to access this endpoint.

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
api_instance = swagger_client.ConsentsApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Consent Uid
consent = swagger_client.ConsentWrite() # ConsentWrite | consentData

try:
    # Update consent
    api_response = api_instance.update_consent(uid, consent)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling ConsentsApi->update_consent: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Consent Uid | 
 **consent** | [**ConsentWrite**](ConsentWrite.md)| consentData | 

### Return type

[**WrappedConsent**](WrappedConsent.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

