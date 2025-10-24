# swagger_client.CountriesApi

All URIs are relative to *https://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_country**](CountriesApi.md#create_country) | **POST** /countries | Create country
[**find_countries**](CountriesApi.md#find_countries) | **GET** /countries | Find country
[**get_country**](CountriesApi.md#get_country) | **GET** /countries/{uid} | Get country
[**update_country**](CountriesApi.md#update_country) | **PUT** /countries/{uid} | Update country


# **create_country**
> WrappedCountry create_country(country)

Create country

You need the countries#write scope to access this endpoint.

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
api_instance = swagger_client.CountriesApi(swagger_client.ApiClient(configuration))
country = swagger_client.CountryWrite() # CountryWrite | Country

try:
    # Create country
    api_response = api_instance.create_country(country)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling CountriesApi->create_country: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **country** | [**CountryWrite**](CountryWrite.md)| Country | 

### Return type

[**WrappedCountry**](WrappedCountry.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **find_countries**
> WrappedWithMetaArrayCountry find_countries(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)

Find country

You need the countries#read scope to access this endpoint.

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
api_instance = swagger_client.CountriesApi(swagger_client.ApiClient(configuration))
limit = 56 # int | Limit (optional)
offset = 56 # int | Offset (optional)
page = 56 # int | Page (optional)
filter = 'filter_example' # str | Filters (optional)
fields = 'fields_example' # str | Fields (optional)
sort = 'sort_example' # str | Sort (optional)
options = 'options_example' # str | Options (optional)

try:
    # Find country
    api_response = api_instance.find_countries(limit=limit, offset=offset, page=page, filter=filter, fields=fields, sort=sort, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling CountriesApi->find_countries: %s\n" % e)
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

[**WrappedWithMetaArrayCountry**](WrappedWithMetaArrayCountry.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_country**
> WrappedCountry get_country(uid, fields=fields, options=options)

Get country

You need the countries#read scope to access this endpoint.

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
api_instance = swagger_client.CountriesApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Country Uid
fields = 'fields_example' # str | Fields to select (optional)
options = 'options_example' # str | Options (optional)

try:
    # Get country
    api_response = api_instance.get_country(uid, fields=fields, options=options)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling CountriesApi->get_country: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Country Uid | 
 **fields** | **str**| Fields to select | [optional] 
 **options** | **str**| Options | [optional] 

### Return type

[**WrappedCountry**](WrappedCountry.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **update_country**
> WrappedCountry update_country(uid, country)

Update country

You need the countries#write scope to access this endpoint.

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
api_instance = swagger_client.CountriesApi(swagger_client.ApiClient(configuration))
uid = 'uid_example' # str | Country Uid
country = swagger_client.CountryWrite() # CountryWrite | Country

try:
    # Update country
    api_response = api_instance.update_country(uid, country)
    pprint(api_response)
except ApiException as e:
    print("Exception when calling CountriesApi->update_country: %s\n" % e)
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **uid** | **str**| Country Uid | 
 **country** | [**CountryWrite**](CountryWrite.md)| Country | 

### Return type

[**WrappedCountry**](WrappedCountry.md)

### Authorization

[ClientCredentials](../README.md#ClientCredentials)

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

