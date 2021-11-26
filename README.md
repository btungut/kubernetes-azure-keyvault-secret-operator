[![Artifact Hub](https://img.shields.io/endpoint?url=https://artifacthub.io/badge/repository/azure-keyvault-secret-operator)](https://artifacthub.io/packages/search?repo=azure-keyvault-secret-operator)
[![Release](https://img.shields.io/github/v/release/btungut/azure-keyvault-secret-operator?include_prereleases&style=plastic)](https://github.com/btungut/azure-keyvault-secret-operator/releases/tag/0.0.1)
[![LICENSE](https://img.shields.io/github/license/btungut/azure-keyvault-secret-operator?style=plastic)](https://github.com/btungut/azure-keyvault-secret-operator/blob/master/LICENSE)

# Azure KeyVault Secret Operator
Easy to use operator which is able to **sync all of the Azure KeyVault contents** into your Kubernetes cluster with **only one manifest**. 

All you need to have a **Service Principal** which is used to access **Azure KeyVault**.
Operator can run on non Azure environments without any kind of other prerequisites like CSI driver, ARC enabling, etc.

## Deployment
Operator may be installed into cluster with helm chart or directly manifests.
### Deploy with Helm Chart
You can find the helm packages in [releases](https://github.com/btungut/azure-keyvault-secret-operator/releases) page. Helm chart supports RBAC and CRD manifests installation. You can pass `--set rbac.enabled=false` if you don't need.

```
helm repo add btungut https://btungut.github.io
helm upgrade -i {RELEASE-NAME} btungut/azure-keyvault-secret-operator
```


### Deploy with manifests 
Please visit the [manifests](https://github.com/btungut/azure-keyvault-secret-operator/tree/master/manifests) folder as you can see there are three manifests.

```
kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/master/manifests/01-crd.yaml
kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/master/manifests/02-rbac.yaml
kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/master/manifests/03-deployment.yaml
```


## What is AzureKeyVault custom resource definition ?
**AzureKeyVault** is a custom resource definition which is being tracked by operator. It is cluster scoped and includes references for Azure KeyVault, service principal and to be synced secrets accross kubernetes cluster.

```
apiVersion: btungut.io/v1
kind: AzureKeyVault
metadata:
  name: contoso
spec:
  name: my-azure-keyvault-name
  resourceGroup: my-azure-resourcegroup
  syncVersion: 1
  servicePrincipal:
    secretName: "my-secret-includes-serviceprincipal"
    secretNamespace: "my-infra-namespace"
    tenantIdField: "tenantid"
    clientIdField: "clientid"
    clientSecretField: "clientsecret"
  objects:
    - name: catalog-db-connectionstring
      type: secret
      copyTo:
        - namespace: services-preprod
          secretName: CatalogDB
        - namespace: services-staging
          secretName: CatalogDB
    - name: catalog-rabbitmq-connectionstring
      type: secret
      copyTo:
        - namespace: services-preprod
          secretName: RabbitMQ
```

`spec.name` and `spec.resourceGroup` needs to point your Azure KeyVault resource.

As we mentioned before, you need to have a service principal that is used to access Azure KeyVault. You need to create a secret **manually** which includes service principal informations. You're completely free for naming of the fields. 

Assuming you have following secret in **my-infra-namespace** namespace;
```
apiVersion: v1
kind: Secret
metadata:
  name: my-secret-includes-serviceprincipal
  namespace: my-infra-namespace
type: Opaque
data:
  clientid: //BASE64 encoded GUID
  clientsecret: //BASE64 encoded password
  tenantid: //BASE64 encoded GUID
```
E.g. : Value of `spec.servicePrincipal.clientSecretField` in AzureKeyVault is pointing the field name of secret. 

## Supported Secret Types
Operator currently supports following types;
- Opaque
- kubernetes.io/dockerconfigjson

You can define a secret type with `spec.objects[].copyTo[].secretType` field which is optional and default value is Opaque if it is not explictly defined.


```
apiVersion: btungut.io/v1
kind: AzureKeyVault
metadata:
  name: contoso
spec:
  ...
  ...
  objects:
    ...
    - name: docker-registry-credentials
      type: secret
      copyTo:
        - namespace: services-preprod
          secretName: registry-credentials
          secretType: "kubernetes.io/dockerconfigjson"
```

## Configuration
You can change the configurable values in [values.yaml](https://github.com/btungut/azure-keyvault-secret-operator/blob/master/helm/values.yaml) of helm chart

```
...
configs:
  LogLevel: "Information"
  EnableJsonLogging: "false"
  ReconciliationFrequency: "00:00:30"
...
```

these are defined in [deployment.yaml](https://github.com/btungut/azure-keyvault-secret-operator/blob/master/manifests/03-deployment.yaml) if you're not using helm chart

```
        env:
          - name: LogLevel
            value: "Information"
          - name: EnableJsonLogging
            value: "false"
          - name: ReconciliationFrequency
            value: "00:00:30"
```

- `LogLevel` might be any of Verbose, Debug, Information, Warning, Error, Fatal
- `EnableJsonLogging` is boolean as string, you can enable it by passing "true" to structured logs.
- `ReconciliationFrequency` couldn't be less than 10 seconds. More information about reconciliation process will be here soon.

