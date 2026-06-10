angular.module('Virtocommerce.UCP')
    .controller('Virtocommerce.UCP.helloWorldController', ['$scope', 'Virtocommerce.UCP.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'UCP';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'ucp.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);
