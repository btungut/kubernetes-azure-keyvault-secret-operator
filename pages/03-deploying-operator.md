---
layout: page
title: "Step 3 : Deploying Operator"
permalink: /example-use-case/03-deploying-operator
---

# Deploying Operator
There are two ways to deploy operator into a kubernetes cluster. In this example, we deploy the operator with helm which is recommended one.

{% include alert.html type="warning" content="It is recommended that you change the namespace with more proper one." %}
{% include alert.html type="warning" content="Changing <i>configs.logLevel</i> to <i>Debug</i> may cause excessive log output." %}

```bash
helm repo add btungut https://btungut.github.io
helm upgrade -i azure-keyvault-secret-operator btungut/kubernetes-azure-keyvault-secret-operator --set configs.logLevel="Debug" --namespace default
```


## Creating Namespaces
As explained in [use-case scenario](./), some namespaces need to be present in kubernetes cluster.

```bash
kubectl create namespace superstore-test
kubectl create namespace newsletter-test
kubectl create namespace superstore-dev
kubectl create namespace newsletter-dev
```

<div class="ex-nav">
  <div class="left-nav">
    <a href="{{ '/example-use-case/02-preparing-service-principal' | prepend: site.baseurl }}">
      << Step 2 : Preparing Service Principal
    </a>
  </div>
  <div class="right-nav">
    <a href="{{ '/example-use-case/04-creating-azurekeyvault-custom-object' | prepend: site.baseurl }}">
      Step 4 : Creating AzureKeyVault Custom Object >>
    </a>
  </div>
</div>
<br>