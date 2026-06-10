angular.module('Virtocommerce.UCP')
    .factory('Virtocommerce.UCP.webApi', ['$resource', function ($resource) {
        return $resource('api/ucp');
    }]);
