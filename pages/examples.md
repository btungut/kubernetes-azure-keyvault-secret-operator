---
layout: page
title: Example Use-Case
permalink: /example-use-case
---
# Example Use-Case
This example use-case aims to show all of the core abilities of Azure KeyVault Secret Operator. Please find the use case scenario below and then apply all of the steps.

## Use-Case Scenario

**SuperStore** is an e-commerce platform which uses Kubernetes for its workloads. Company plans to keep credentials in Azure KeyVault as listed below;

| Secret Name             | Description                                                                                                                                |
|:----------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------|
| catalogdb         | MSSQL connection string as ready to use directly|
| amqp-username    | Username of AMQP product|
| amqp-password | Password of AMQP product|
| docker-config-json      | Private docker registry credential for pulling images.|


### Secrets Need To Be Created

However, every namespace in the Kubernetes needs different kubernetes secret. Following pseudo secret yamls should be applied by operator.

<br>Below secret should be present in **superstore-test** namespace.
```yaml
...
kind: Secret
name: catalog-api
data:
  catalogdb-connectionstring: "{catalogdb}"
```

<br>Below secret should be present in **every namespace which ends with "test"**
```yaml
...
kind: Secret
name: amqp
data:
  amqp-connectionstring: "amqp://{amqp-username}:{amqp-password}@brokersvc:5672/"
```

<br>Below secret should be present in **every namespace which doesn't include "kube"**
```yaml
...
kind: Secret
name: docker-registry-credential
type: kubernetes.io/dockerconfigjson
data:
  .dockerconfigjson: "{docker-pull-secret}"
```

<br><br>

<div class="section-index">
    <hr class="panel-line">
    <div class="entry">
      <h5><a href="01-preparing-azure-resources">Step 1 : Preparing Azure Resources</a></h5>
      <p></p>
    </div>
    <div class="entry">
      <h5><a href="02-preparing-service-principal">Step 2 : Preparing Service Principal</a></h5>
      <p></p>
    </div>
    <div class="entry">
      <h5><a href="03-deploying-operator">Step 3 : Deploying Operator</a></h5>
      <p></p>
    </div>
    <div class="entry">
      <h5><a href="04-creating-azurekeyvault-custom-object">Step 4 : Creating AzureKeyVault Object</a></h5>
      <p></p>
    </div>
    <div class="entry">
      <h5><a href="05-review">Step 5 : Review</a></h5>
      <p></p>
    </div>
</div>