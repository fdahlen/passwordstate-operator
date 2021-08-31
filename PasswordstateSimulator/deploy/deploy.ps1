$namespace = "simulator"
$image = "ghcr.io/fdahlen/passwordstate-operator-simulator:latest"
$url = "http://passwordstatesimulator.$namespace.svc.cluster.local"

(Get-Content deployment.yaml).
    replace('%%image%%', $image).
    replace('%%url%%', $url) |
    kubectl apply -n $namespace -f -

(Get-Content service.yaml) |
    kubectl apply -n $namespace -f -