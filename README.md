# Azure KeyVault Secret Operator
Easy to use operator which is able to **sync all of the Azure KeyVault contents** into your Kubernetes cluster with **only one manifest**. 

All you need to have a **Service Principal** which is used to access **Azure KeyVault**.
Operator can run on non Azure environments without any kind of other prerequisites like CSI driver, ARC enabling, etc.

## Deployment
Operator may be installed into cluster with helm chart or directly manifests.
### Deploy with Helm Chart
You can find the helm packages in [releases]([https://link](https://github.com/btungut/azure-keyvault-secret-operator/releases)) page. Helm chart supports RBAC and CRD manifests installation. You can pass `--set rbac.enabled=false` if you don't need.

From published release;

`helm upgrade -i azure-keyvault-secret-operator https://github.com/btungut/azure-keyvault-secret-operator/releases/download/0.0.1/chart.tgz`

or you can clone the repository

`git clone https://github.com/btungut/azure-keyvault-secret-operator.git .`

`helm upgrade -i azure-keyvault-secret-operator ./helm`

### Deploy with manifests 
Please visit the **manifests** folder as you can see there are three manifests.

`kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/baseline/manifests/01-crd.yaml`

`kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/baseline/manifests/02-rbac.yaml`

`kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/baseline/manifests/03-deployment.yaml`



## What is AzureKeyVault custom resource definition ?
**AzureKeyVault** is a custom resource definition which is being tracked by operator. It is cluster scoped and includes references for Azure KeyVault, service principal and to be synced kubernetes clusters.

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

As we mentioned before, you need to have a service principal that is used to access Azure KeyVault. You need to create a secret **manually** which includes service principal informations. You're completely free for naming of fields. 

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
You can change the configurable values in values.yaml of helm chart

```
...
configs:
  LogLevel: "Information"
  EnableJsonLogging: "false"
  ReconciliationFrequency: "00:00:30"
...
```

these are defined in deployment if you're not using helm chart

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

