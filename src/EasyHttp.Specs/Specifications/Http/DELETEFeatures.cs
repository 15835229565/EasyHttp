﻿using System;
using EasyHttp.Http;
using EasyHttp.Specs.Helpers;
using Machine.Specifications;

namespace EasyHttp.Specs.Specifications.Http
{
    [Subject("HttpClient")]
    public class when_making_a_DELETE_request_with_a_valid_uri 
    {
        Establish context = () =>
        {
            httpClient = new HttpClient();
            httpClient.Request.Accept = HttpContentTypes.ApplicationJson;
            
            // First create customer in order to then delete it
            guid = Guid.NewGuid();

            httpClient.Put(string.Format("{0}/{1}", TestSettings.CouchDbDatabaseUrl, guid),
                          new Customer() {Name = "ToDelete", Email = "test@test.com"}, HttpContentTypes.ApplicationJson);

            response = httpClient.Response;

            rev = response.DynamicBody.rev;
        };

        Because of = () =>
        {

            httpClient.Delete(String.Format("{0}/{1}?rev={2}", TestSettings.CouchDbDatabaseUrl, guid, rev));
            response = httpClient.Response;
        };

        It should_delete_the_specified_resource = () =>
        {
            bool ok = response.DynamicBody.ok;

            string id = response.DynamicBody.id;

            ok.ShouldBeTrue();

            id.ShouldNotBeEmpty();

        };


        static HttpClient httpClient;
        static dynamic response;
        static string rev;
        static Guid guid;
    }
}