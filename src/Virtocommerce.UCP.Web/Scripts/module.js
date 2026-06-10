// Call this to register your module to main application
var moduleName = 'Virtocommerce.UCP';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.UCPState', {
                    url: '/ucp',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'blade1',
                                controller: 'Virtocommerce.UCP.helloWorldController',
                                template: 'Modules/$(Virtocommerce.UCP)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/ucp',
                icon: 'fa fa-cube',
                title: 'UCP',
                priority: 100,
                action: function () { $state.go('workspace.UCPState'); },
                permission: 'ucp:access',
            };
            mainMenuService.addMenuItem(menuItem);
        }
    ]);
