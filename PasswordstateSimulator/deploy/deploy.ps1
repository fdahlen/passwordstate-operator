$namespace = "passwordstate-simulator"
$image = "xxxxx"

(Get-Content deployment.yaml).
    replace('%%image%%', $image) |
    kubectl apply -n $namespace -f -

(Get-Content service.yaml) |
    kubectl apply -n $namespace -f -