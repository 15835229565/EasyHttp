﻿using EasyHttp.Http;
using Machine.Specifications;

namespace EasyHttp.Specs.Specifications.HttpMethods
{
    [Subject("HttpClient")]
    public class when_creating_a_new_instance
    {
        Because of = () =>
        {
            _httpClient = new HttpClient(); ;
        };

        It should_return_new_instance = () => _httpClient.ShouldNotBeNull();

        static HttpClient _httpClient;
    }
}