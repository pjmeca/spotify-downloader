#!groovy

pipeline {
    agent any
    environment {
        IMAGE_NAME = 'pjmeca/spotify-downloader'
        DOCS_IMAGE_NAME = 'pjmeca/spotify-downloader-docs'
    }
    stages {
        stage('Docker Build Edge') {
            steps {
                sh 'docker build -t ${IMAGE_NAME}:edge ./SpotifyDownloader'
            }
        }
        stage('Docs Build Prod') {
            steps {
                sh 'docker build -t ${DOCS_IMAGE_NAME} -f mkdocs/Dockerfile --target prod mkdocs'
            }
        }
        stage('Docker Push Edge') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'docker-hub-credentials',
                                                  usernameVariable: 'DOCKER_USER',
                                                  passwordVariable: 'DOCKER_PASS')]) {
                    sh '''
                        echo "$DOCKER_PASS" | docker login -u "$DOCKER_USER" --password-stdin
                        docker push ${IMAGE_NAME}:edge
                    '''
                }
            }
        }
        stage('Docs Deploy Prod') {
            steps {
                sh '''
                    docker rm -f spotify-downloader-docs
                    docker run -d --name spotify-downloader-docs --restart unless-stopped -p 14666:80 ${DOCS_IMAGE_NAME}'
                '''
            }
        }
    }
}
