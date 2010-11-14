﻿using EasyHttp.Http;
using EasyHttp.Specs.Helpers;
using Machine.Specifications;

namespace EasyHttp.Specs.Specifications.Http
{
    [Subject("HttpClient")]
    public class when_making_a_POST_request_with_valid_uri_and_valid_data_and_content_type_set_to_application_json
    {
        Establish context = () =>
        {
            _httpClient = new HttpClient();
            _httpClient.Request.Accept = HttpContentTypes.ApplicationJson;

        };

        Because of = () =>
        {

            _httpClient.Post(TestSettings.CouchDbDatabaseUrl, new Customer() { Name = "Hadi", Email = "test@test.com" }, HttpContentTypes.ApplicationJson);

            response = _httpClient.Response;
        };


        It should_succeed = () =>
        {
            bool ok = response.DynamicBody.ok;

            string id = response.DynamicBody.id;

            ok.ShouldBeTrue();

            id.ShouldNotBeEmpty();
        };



        static HttpClient _httpClient;
        static dynamic response;
    }
}